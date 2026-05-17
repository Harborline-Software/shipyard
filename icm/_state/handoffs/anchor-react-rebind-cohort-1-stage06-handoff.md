---
title: Anchor React Bridge Rebind Cohort 1 — Stage 06 Hand-off
workstream: TBD (XO recommends W#74 — register in `active-workstreams.md` before kickoff)
cluster: anchor-react-rebind (cross-package: `accelerators/bridge/Sunfish.Bridge/` + `apps/anchor-react/`)
pipeline: sunfish-feature-change
authored-by: XO
authored-at: 2026-05-17T15-00Z
status: ready-to-build
co-pre-authorized: requested
co-pre-authorized-rationale: |
  Cohort 1 is the first cohort under the Tauri-first pivot ratified by CO
  2026-05-17T14-30Z. All four PRs are mechanical rebind work (Bridge
  endpoint family lifted from the cockpit pattern + frontend fetcher swap
  inside existing TanStack hooks). No novel security surface beyond
  tenant-scoping (which is server-derived per CO Q1 ruling); no ADR or
  Anchor-substrate touch. PR 1 carries the only judgment surface
  (first cluster-endpoint policy decision); subsequent PRs mirror it.
  Pattern-005 + pattern-006 + pattern-007 apply for the substrate
  pieces; the rebind shape itself is a candidate for pattern-009 after
  three clean shippings.
co-pre-authorized-scope:
  - PR 1 (PropertiesPage rebind + cluster-endpoint pattern) — security-engineering SPOT-CHECK required pre-merge; pre-auth conditional on council clean
  - PR 2 (LeasesPage + LeaseDetailPage rebind bundled) — pre-auth if PR 1 council clean and pattern matched
  - PR 3 (MaintenancePage rebind) — pre-auth; reuses existing `/api/v1/cockpit/work-orders/*` family; mechanical
  - PR 4 (ERPNext deprecation marks + E2E smoke + ledger flip) — ledger-flip PR; CO sees regardless of pre-auth (per ruling Step 4)
  - PR-count maximum: 4 (workstream re-evaluation if scope grows)
  - PR-deviation flag triggers immediate CO escalation for that PR
merge-tier: pre-authorized-pending-CO-ratification
depends-on:
  - blocks-properties (W#62 PropertyUnit substrate) — shipped ✓
  - blocks-leases (W#27 Party retrofit) — shipped ✓
  - blocks-work-orders / blocks-maintenance — shipped ✓ (Bridge `/api/v1/cockpit/work-orders/*` family live on main)
  - Bridge cockpit-endpoint pattern (`accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitEndpoints.cs`) — shipped ✓
  - `apps/anchor-react/src/cockpit/api.ts` client pattern — shipped ✓
  - Tauri-first pivot ratification (CO 2026-05-17T14-30Z) — ratified ✓
spec-source: |
  - `icm/02_architecture/anchor-react-bridge-rebind-roadmap.md` (§2 surface inventory, §4 Cohort 1 table, §5 cross-cutting concerns)
  - ADR 0088 — Path II: Anchor as all-in-one local-first runtime (§1 cluster decomposition, §3 frontend boundary)
  - ADR 0031 — Bridge hybrid hosted-node-as-SaaS (cockpit auth + tenant context conventions)
  - `_shared/engineering/standing-approved-patterns.md` (pattern-005, pattern-006, pattern-007; candidate pattern-009)
  - `_shared/engineering/xo-ruling-workstream-pre-authorization.md` (Option B pilot mechanism)
estimated-effort: ~5–7h dev across 4 PRs (~1h PR 1, ~2h PR 2, ~1.5h PR 3, ~1.5h PR 4 incl. E2E smoke)
PR-count: 4
pre-merge-council:
  security-engineering: SPOT-CHECK required on PR 1 only; subsequent PRs mechanical mirrors
  dotnet-architect: NOT required (mechanical Bridge endpoint addition; pattern lifted verbatim from `CockpitEndpoints.cs`)
  frontend-reviewer: NOT required (mechanical fetcher swap inside existing TanStack hooks; no new state-management or UI patterns)
license-posture: MIT clean-room (all frontend code is original; no FOSS source studied for this rebind)
---

# Hand-off — Anchor React Bridge Rebind Cohort 1 (Properties + Leases + Maintenance)

**From:** XO (research session)
**To:** sunfish-PM (COB) — fallback: dev
**Workstream:** TBD — XO recommends **W#74 Anchor React Rebind Cohort 1**; register the source `W74-anchor-react-rebind-cohort-1.md` row in `icm/_state/workstreams/` and re-render the ledger before COB kickoff (per `feedback_never_add_workstream_rows_directly_to_ledger`).
**Pipeline:** `sunfish-feature-change`
**Ratifications applied:**
- CO 2026-05-17T14-30Z Tauri-first pivot ratification — Anchor React shifts its data plane from `/api/v1/erpnext/*` to `/api/v1/<cluster>/*` via Bridge; ERPNext stays as Pass 1 migration source only.
- CO ratified roadmap Q1: **EntityTag filter is server-derived from tenant context** (auth claims / session), NOT a frontend query param.
- CO ratified roadmap Q2: **ERPNext passthrough kept for one milestone** (i.e., through Cohort 4); deleted wholesale at Cohort 4 cleanup. Cohort 1 marks the four routes it consumes `@deprecated`.
- CO ratified roadmap Q3 (confirm at hand-off review): **per-page PR bundling** — one PR per page, with Bridge endpoint + frontend rebind landed together. XO's recommendation; the hand-off is structured around it. **If CO prefers a different bundling at hand-off review, halt and re-author.**

---

## 1. Context

### 1.1 Why Cohort 1 ships first

Per ADR 0088 §1 (Path II), Anchor is the all-in-one local-first runtime. Its data plane should be **native `blocks-*` clusters**, NOT ERPNext at runtime. The Phase 1 + 2 + 3 cluster work has shipped the native domain (`blocks-properties` with PropertyUnit substrate, `blocks-leases` post-Party retrofit, `blocks-work-orders`). What remains is the frontend rebind: `apps/anchor-react/src/api/erpnext.ts` is still the data plane for nine out of twelve pages.

Cohort 1 is the **foundational cluster** of the rebind. It targets the three pages whose `blocks-*` predecessors are already shipped on main and have repository surfaces (`IPropertyRepository`, `ILeaseService`, `IMaintenanceService`) the Bridge can consume directly. The four pages in scope are:

| # | Page (in `apps/anchor-react/src/pages/`) | Current ERPNext call | Target Bridge endpoint | Read/Write |
|---|---|---|---|---|
| 1 | `PropertiesPage.tsx` | `getProperties()` → `/api/v1/erpnext/properties` | `GET /api/v1/properties` (NEW top-level family) | Read |
| 2 | `LeasesPage.tsx` | `getLeases()` → `/api/v1/erpnext/leases` | `GET /api/v1/leases` (NEW top-level family) | Read |
| 3 | `LeaseDetailPage.tsx` | `getLease(name)` + `getPayments()` → `/api/v1/erpnext/leases/{name}` + `/api/v1/erpnext/payments` | `GET /api/v1/leases/{id}` + (payments deferred to Cohort 2) | Read |
| 4 | `MaintenancePage.tsx` | `getMaintenanceTickets()` + `createMaintenanceTicket()` + `updateMaintenanceTicket()` → `/api/v1/erpnext/maintenance/*` | `GET/POST /api/v1/cockpit/work-orders/*` (existing family; PR 3 adds `POST /` for create) | Read + Write |

After Cohort 1 lands, those four pages can render without ERPNext running.

### 1.2 What Cohort 1 ships

Per the roadmap §4 Cohort 1 table:

1. **`/api/v1/properties` top-level Bridge endpoint family** (NEW). Returns property summaries scoped by `ITenantContext`. **EntityTag filter is server-derived** from tenant context per CO Q1 ruling — frontend does not pass it.
2. **`/api/v1/leases` top-level Bridge endpoint family** (NEW). `GET /` returns active leases; `GET /{id}` returns lease detail. Server-derived tenant scoping; no frontend query-param required.
3. **`POST /api/v1/cockpit/work-orders/` create endpoint** (extends the EXISTING family). Reuses the cockpit work-orders surface that already lives in `accelerators/bridge/Sunfish.Bridge/Cockpit/WorkOrdersEndpoint.cs`. **Caveat:** because this PR touches `accelerators/bridge/Sunfish.Bridge/Cockpit/**`, it is on the "always-needs-full-pipeline" list per `standing-approved-patterns.md` §"What's explicitly NOT a standing pattern". Pre-authorization on PR 3 is conditional on whether CO accepts the cockpit-touch exception OR XO refactors PR 3 to put the new `POST` under a non-cockpit `/api/v1/maintenance/work-orders/` route. **See Halt H4 below — XO recommends the non-cockpit-route variant.**
4. **`apps/anchor-react/src/api/cockpit.ts` or new top-level clients** updated to expose the new endpoint families (`getProperties()` from cluster, `getLeases()` + `getLease()` from cluster, `createWorkOrder()`).
5. **Hooks rebind:** `useProperties.ts` + `useLeases.ts` swap fetchers to point at the new clients. Same TanStack query-key shapes (per roadmap §5.3 — no new state-management patterns).
6. **ERPNext route deprecation marks (PR 4):** the four `erpnext.ts` exports consumed by Cohort 1 (`getProperties`, `getLeases`, `getLease`, `getMaintenanceTickets`/`createMaintenanceTicket`/`updateMaintenanceTicket`) get JSDoc `@deprecated` tags with a console warning in dev mode pointing at the new endpoint.
7. **E2E smoke test (PR 4):** Playwright CDP test (per `feedback_anchor_smoke_test_playwright_cdp`) brings up Anchor with ERPNext NOT running; cycles through all four pages; asserts each renders with seed data and the network panel shows zero requests to `/api/v1/erpnext/*` for the four pages in scope.
8. **`apps/docs/anchor/anchor-react-rebind.md` running log** (PR 4): one section per cohort tracking which pages are rebound, the Bridge endpoints they call, and the ERPNext deprecation timeline.
9. **Ledger flip (PR 4):** W#74 row from `building` → `built` via the source `W74-*.md` file + render-ledger.

### 1.3 What Cohort 1 does NOT ship

- **Financial pages** (`AccountingPage`, `PLReport`, `RentRoll`, `RentCollectionPage`) — Cohort 2 / Cohort 3 scope.
- **Payment recording** (`RentCollectionPage.recordPayment`) — gated on `blocks-financial-payments` cluster (future workstream); Cohort 2 RB-8.
- **Multi-device CRDT sync** — out of scope per roadmap §1.
- **In-process IPC (Phase B)** — Anchor → Bridge stays over HTTP per ADR 0088 §3.
- **ERPNext route deletion** — Cohort 4 (RB-12). Cohort 1 marks routes `@deprecated`; deletion happens after every consumer page is rebound.
- **Wave/Rentler import migration** — separate `erpnext-migration-orchestrator-stage06-handoff.md`.
- **`apps/anchor-react/src/pages/RentRoll.tsx`** — already calls `/api/v1/reports/rent-roll` (NOT `/erpnext/`); see Halt H3.

### 1.4 Tauri-first pivot — auth flow at the frontend

Per the pivot ratification, Anchor React runs inside Tauri's WebView; Tauri shell handles credential injection. Bridge endpoints continue to read the authenticated user via `ITenantContext` (existing convention; same as cockpit endpoints). The frontend does NOT manage auth headers — it uses `credentials: 'include'` per the existing `cockpit/api.ts` pattern. The Tauri shell injects the Bridge base URL at startup; the frontend uses relative paths (`/api/v1/properties` etc.) and the WebView resolves through the shell's base URL. **Verify this assumption at Halt H2 before PR 1 ships.**

### 1.5 Why CO sees PR 4 regardless of pre-auth

PR 4 is the **ledger-flip PR** for W#74. Per the pre-authorization ruling (§Step 4):

> Even under pre-authorization, CO is brought into the loop if ANY of: **Ledger-flip PR** (final PR of the workstream — CO always sees workstream completions) [...]

PR 4 is also the smoke-test PR. Both reinforce the rationale: this PR is where CO sees the rebind has actually landed and Anchor can stand on its own without ERPNext.

---

## 2. Pre-build checklist (COB executes before opening PR 1)

Run each step; halt on any unexpected state.

### 2.1 Confirm dependencies on main

```bash
# All four prerequisite clusters must be on main:
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-properties/Services/IPropertyRepository.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-leases/Services/ILeaseService.cs
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/packages/blocks-maintenance/ 2>&1 | head -5
ls /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/Cockpit/CockpitEndpoints.cs
ls /Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/Cockpit/WorkOrdersEndpoint.cs
ls /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/web/src/cockpit/api.ts
ls /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/web/src/pages/PropertiesPage.tsx
ls /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/web/src/pages/LeasesPage.tsx
ls /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/web/src/pages/LeaseDetailPage.tsx
ls /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/web/src/pages/MaintenancePage.tsx
```

Expected: all exist. If any missing, **STOP** — drop a `cob-question-*` beacon naming the missing dependency.

### 2.2 Confirm cockpit endpoint pattern is the right template

Read `accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitEndpoints.cs` + `PropertyDetailEndpoint.cs` + `WorkOrdersEndpoint.cs` end-to-end. The cockpit pattern is the cited template; Cohort 1's new top-level endpoints **mirror this shape** with one key deviation: **the route group is NOT under `/api/v1/cockpit` and does NOT inherit `CockpitPolicy`**. Reasons:

1. `CockpitPolicy` requires `role ∈ {owner, spouse}` per `CockpitPermissions.CanEnterCockpit(...)`. Cohort 1 pages must be reachable by any authenticated tenant user, not only the cockpit role set.
2. The cockpit route family is a deliberately narrow surface (`CockpitEndpoints.cs` comment); it is on the "always-needs-full-pipeline" list per the standing-patterns catalog.

**Cohort 1's new top-level families use a fresh authorization policy** — `AuthenticatedTenantPolicy` (working name) — that requires only `RequireAuthenticatedUser()` plus `ITenantContext` resolution. **See PR 1 §3.1 for the policy spec.** If the policy already exists under a different name in `foundation-authorization`, prefer the existing name (verify before PR 1; otherwise add the policy in PR 1 alongside the endpoint family).

### 2.3 Confirm Tauri shell Bridge base URL is wired

```bash
grep -rn "BRIDGE_BASE_URL\|baseUrl\|VITE_BRIDGE" /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/web/ 2>/dev/null | head -10
grep -rn "tauri.conf\|allowlist\|http" /Users/christopherwood/Projects/Harborline-Software/sunfish/apps/desktop/ 2>/dev/null | head -10
```

Expected: either the Tauri shell injects the Bridge base URL at startup (frontend uses relative paths) OR the shell maps `/api/v1/*` to the Bridge process. **If neither is in place**, halt on H2 — XO must clarify the Tauri-shell wiring before PR 1 ships.

### 2.4 Confirm no parallel-session PRs touch the same surface

```bash
gh pr list --state open --search "anchor-react in:title,body"
gh pr list --state open --search "/api/v1/properties OR /api/v1/leases in:title,body"
gh pr list --state open --search "WorkOrdersEndpoint OR MaintenancePage in:title,body"
```

Expected: empty (or only this hand-off's own PRs). If anything else is open and would conflict, file `cob-question-*`.

### 2.5 Confirm `but status` / `git status` is clean

Current branch should be `main` (or a fresh worktree from `main` per `feedback_worktree_base_main_not_gitbutler`).

### 2.6 Confirm the workstream row exists

```bash
grep -n "W#74\|anchor-react-rebind-cohort-1" /Users/christopherwood/Projects/Harborline-Software/shipyard/icm/_state/active-workstreams.md
ls /Users/christopherwood/Projects/Harborline-Software/shipyard/icm/_state/workstreams/W74-* 2>&1
```

Expected: workstream registered as `ready-to-build`. If absent, **STOP** — XO must register the workstream first (the hand-off file referencing W#74 is not enough on its own; the source `W74-*.md` file plus render-ledger must run).

### 2.7 Confirm the pre-authorization frontmatter status

The hand-off ships with `co-pre-authorized: requested`. CO ratifies (or declines) at hand-off review. **COB must NOT open PRs under the pre-authorization shortcut until the frontmatter says `co-pre-authorized: granted`.** If it says `declined`, fall back to the per-PR-CO-click model (still ship the work — just don't arm auto-merge without CO click).

### 2.8 Read the supporting docs once

Skim:
- `icm/02_architecture/anchor-react-bridge-rebind-roadmap.md` (full; ~230 lines)
- `accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitEndpoints.cs` + `PropertyDetailEndpoint.cs` + `WorkOrdersEndpoint.cs` (cited templates)
- `apps/anchor-react/src/cockpit/api.ts` (client-side template)
- `apps/anchor-react/src/hooks/useProperties.ts` + `useLeases.ts` (fetcher patterns)
- `_shared/engineering/standing-approved-patterns.md` (pattern-005, pattern-006, pattern-007 in particular)
- `_shared/engineering/xo-ruling-workstream-pre-authorization.md` (auto-merge flow under pre-auth)

---

## 3. Per-PR deliverables

Cohort 1 splits into **4 PRs**:

- **PR 1** — `/api/v1/properties` Bridge endpoint family + `PropertiesPage` rebind (cluster-endpoint pattern; security-engineering SPOT-CHECK required)
- **PR 2** — `/api/v1/leases` Bridge endpoint family + `LeasesPage` + `LeaseDetailPage` rebind (bundled)
- **PR 3** — `POST /api/v1/maintenance/work-orders/` create endpoint OR cockpit `POST /` extension (CO choice; XO recommends non-cockpit route) + `MaintenancePage` rebind
- **PR 4** — ERPNext deprecation marks + E2E smoke test + `apps/docs/anchor/anchor-react-rebind.md` + ledger flip (the ledger-flip PR; CO sees this one)

PRs 1 → 2 → 3 → 4 should be **sequential** (XO recommendation) for ease of CO review. The roadmap notes parallel work is possible within Cohort 1 (per §7); the sequential discipline is for reviewability, not technical necessity.

---

### PR 1 — `/api/v1/properties` Bridge endpoint family + `PropertiesPage` rebind

**Estimated effort:** ~1.5h
**Scope:** new Bridge route family `/api/v1/properties`; new `AuthenticatedTenantPolicy` (if absent); new top-level frontend client; `useProperties.ts` fetcher swap; `PropertiesPage.tsx` type-shape adjustment
**Commit subject:** `feat(anchor-react,bridge): rebind PropertiesPage to /api/v1/properties cluster endpoint (cohort 1 PR 1)`
**Branch:** `cob/anchor-react-rebind-properties`
**Pre-merge council:** **security-engineering SPOT-CHECK required** — first cluster-endpoint pattern; tenant-isolation must be correctly enforced via server-derived `ITenantContext`. Council scope:
  - Does the new route group correctly enforce authentication?
  - Is `TenantId` resolved from `ITenantContext` (NOT from a query param or header)?
  - Does EntityTag filter (when CO ratifies it as live) derive from the same server context, not the client?
  - Does the policy correctly reject anonymous callers?
  - Council out: `xo-spot-check-2026-05-XXTHH-MMZ-anchor-react-rebind-properties.md` in `coordination/inbox/`.

#### 3.1 Bridge endpoint family — `/api/v1/properties`

New file: `accelerators/bridge/Sunfish.Bridge/Properties/PropertiesEndpoints.cs`.

Pattern lifted from `CockpitEndpoints.cs` with two adjustments:

1. **Route prefix is `/api/v1/properties` (top-level), not `/api/v1/cockpit/properties`.** This places the endpoint outside the `cockpit/` folder (and outside the always-needs-full-pipeline list).
2. **Authorization policy is `AuthenticatedTenantPolicy`, not `CockpitPolicy`.** Definition (new — adds to `accelerators/bridge/Sunfish.Bridge/Authorization/AuthenticatedTenantPolicy.cs` OR reuses an existing equivalent in `foundation-authorization` — verify in pre-build §2.2):

   ```text
   AuthenticatedTenantPolicy:
     - RequireAuthenticatedUser()
     - RequireAssertion(ctx => ctx.User.Claims has a resolvable tenant_id claim
                               OR the configured fallback tenant accessor returns non-null)
   ```

   The policy is **shared across Cohort 1's new top-level families** (`/api/v1/properties`, `/api/v1/leases`, future `/api/v1/maintenance/*`). Register once in PR 1; PRs 2 + 3 reuse.

3. **Endpoint shape**: a single `GET /` that returns the property summary list for the authenticated tenant. Wire-format mirrors `CockpitPropertyList` / `PropertySelectorListDto` from the cockpit endpoint, **with EntityTag-derived filtering applied server-side per CO Q1**.

   Server-side EntityTag application:
   - If `ITenantContext` exposes a resolved `EntityTag` (e.g., via a `CurrentEntityTag` property OR via a tenant-bound `IEntityTagResolver` service), the list query is filtered to properties where `Property.EntityTag == ctx.CurrentEntityTag`.
   - If `EntityTag` resolution is absent / null on the tenant context, return the full tenant-scoped list (existing behavior).
   - **Frontend MUST NOT pass an `?entityTag=` query param** — server is the source of truth.

   **Halt note:** `EntityTag` was specified by the W#64 hand-off (`blocks-properties-entity-tag-stage06-handoff.md`) but is NOT confirmed shipped to main as of this hand-off's authoring (verified 2026-05-17 via search returning only the hand-off file, no `EntityTag` references in `packages/blocks-properties/Models/`). If `Property.EntityTag` does not yet exist as a field, the server-side filter is a no-op (returns the unfiltered tenant-scoped list); add a TODO comment naming W#64 and proceed. **Do not block on this.** When W#64 ships, a follow-on touch-up (`pattern-008`) wires the filter.

4. **DTO shape:**
   ```text
   PropertyListDto:
     properties: PropertySummaryDto[]

   PropertySummaryDto:
     propertyId: string             // Property.Id.Value
     displayName: string            // Property.DisplayName
     kind: string                   // Property.Kind enum.ToString()
     addressLine1: string?          // Property.Address.Line1 (best-effort)
     city: string                   // Property.Address.City
     region: string                 // Property.Address.Region (state/province)
     unitCount: int                 // Count of PropertyUnits or 0 if not yet wired
     status: string                 // "Active" / "Vacant" / "Maintenance" / "Sold"
                                    //   — derived from blocks-properties.Property.Status
                                    //   if it has one, else "Active" as default v1
     entityTag: string?             // server-side echo for read-only display; do NOT use for filtering
   ```

5. **Method signature** (handler):
   ```text
   internal static async Task<Ok<PropertyListDto>> HandleListPropertiesAsync(
       ITenantContext tenantContext,
       IPropertyRepository properties,
       IEntityTagResolver? entityTagResolver, // optional; null if W#64 not shipped
       CancellationToken ct)
   ```

   Wire-up:
   ```text
   var tenant = tenantContext.TenantId;
   var entityTag = entityTagResolver?.GetCurrentEntityTag();
   var rows = entityTag is null
       ? await properties.ListByTenantAsync(tenant, includeDisposed: false, ct)
       : await properties.ListByEntityTagAsync(tenant, entityTag, includeDisposed: false, ct);
   var items = rows.Select(ToSummaryDto).ToArray();
   return TypedResults.Ok(new PropertyListDto(items));
   ```

   `ListByEntityTagAsync` is added by W#64; until then, fall through to `ListByTenantAsync` and TODO-comment the EntityTag path.

6. **Registration:** new file `accelerators/bridge/Sunfish.Bridge/Properties/PropertiesEndpointsExtensions.cs`:
   ```text
   public static IEndpointRouteBuilder MapPropertiesEndpoints(this IEndpointRouteBuilder app)
   {
     var group = app.MapGroup("/api/v1/properties").RequireAuthorization(AuthenticatedTenantPolicyName);
     group.MapGet("/", HandleListPropertiesAsync).WithName("ListProperties");
     return app;
   }
   ```

   And the `AuthenticatedTenantPolicy` registration goes alongside in `Authorization/AuthenticatedTenantPolicy.cs`:
   ```text
   public const string PolicyName = "AuthenticatedTenantPolicy";

   public static AuthorizationOptions AddAuthenticatedTenantPolicy(this AuthorizationOptions options)
   {
     options.AddPolicy(PolicyName, policy =>
     {
       policy.RequireAuthenticatedUser();
       // Optional tenant assertion if your context resolver is strict.
     });
     return options;
   }
   ```

7. **Wire into `Sunfish.Bridge` `Program.cs`** (or `Startup.cs` equivalent):
   - Add `options.AddAuthenticatedTenantPolicy()` in the `AddAuthorization` callback (next to `AddCockpitPolicy()`).
   - Add `app.MapPropertiesEndpoints()` next to `app.MapCockpitEndpoints()`.

#### 3.2 Frontend client — `apps/anchor-react/src/api/properties.ts`

New file. Mirrors the `cockpit/api.ts` style (relative URL, `credentials: 'include'`, throw on non-2xx):

```text
export interface PropertySummary {
  propertyId: string
  displayName: string
  kind: string
  addressLine1: string | null
  city: string
  region: string
  unitCount: number
  status: 'Active' | 'Vacant' | 'Maintenance' | 'Sold'
  entityTag: string | null
}

export interface PropertyList {
  properties: PropertySummary[]
}

export async function getProperties(): Promise<PropertyList> {
  const resp = await fetch('/api/v1/properties', { credentials: 'include' })
  if (!resp.ok) {
    throw new Error(`Failed to load properties: ${resp.status} ${resp.statusText}`)
  }
  return (await resp.json()) as PropertyList
}
```

Naming note: the function is also `getProperties`. The collision with `apps/anchor-react/src/api/erpnext.ts → getProperties()` is intentional and resolved by the import path (`@/api/properties` vs `@/api/erpnext`). The hook (§3.3) imports from the new path.

#### 3.3 Hook rebind — `apps/anchor-react/src/hooks/useProperties.ts`

```text
import { useQuery } from '@tanstack/react-query'
import { getProperties } from '@/api/properties'    // changed from '@/api/erpnext'
import { useCompanyStore } from '@/stores/companyStore'

export function useProperties() {
  const activeCompany = useCompanyStore((s) => s.activeCompany)
  return useQuery({
    queryKey: ['properties', activeCompany],         // unchanged; preserves cache
    queryFn: getProperties,
    retry: 2,
    retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10000),
  })
}
```

Per roadmap §5.3: **same TanStack query-key shape, just swap the fetcher.** The cache continues to work; consumers do not re-render unnecessarily.

#### 3.4 Page rebind — `apps/anchor-react/src/pages/PropertiesPage.tsx`

The current page reads `property.name`, `property.property_name`, `property.address_line_1`, `property.city`, `property.state`, `property.units`, `property.status` — ERPNext field names. The new DTO uses different names. The page must be edited to:

1. Change the import: `import type { Property } from '@/api/erpnext'` → `import type { PropertySummary } from '@/api/properties'`.
2. Update the `useProperties()` consumer: `const { data, ... }` — `data` is now `PropertyList | undefined`, not `Property[] | undefined`. Adjust: `const properties = data?.properties`.
3. Update card body fields:
   - `p.name` → `p.propertyId`
   - `p.property_name` → `p.displayName`
   - `p.address_line_1` → `p.addressLine1`
   - `p.city` → `p.city`
   - `p.state` → `p.region`
   - `p.units` → `p.unitCount`
   - `p.status` → `p.status` (same shape; same values)
4. Update the empty-state copy: `"No properties found. Create your first Property record in ERPNext."` → `"No properties found. Add a property in the cockpit to get started."` — remove the ERPNext-specific guidance.
5. Update `statusVariant` to accept `PropertySummary['status']` (same union).

#### 3.5 Tests

- **Bridge endpoint integration test** — new file `accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/Properties/PropertiesEndpointsTests.cs`:
  - `ListProperties_AuthenticatedTenant_ReturnsTenantScopedRows`
  - `ListProperties_UnauthenticatedCaller_Returns401`
  - `ListProperties_WrongTenant_ReturnsEmptyList`
  - `ListProperties_WithEntityTagOnContext_FiltersByEntityTag` (skip / TODO until W#64 ships)
  - `ListProperties_WithoutEntityTagOnContext_ReturnsFullTenantList`
  - `ListProperties_EmptyTenant_ReturnsEmptyArrayNotNull`
  - `ListProperties_PropertyWithoutAddressLine_ReturnsNullField` (DTO null safety)
  - `ListProperties_StatusMapping_AllValuesProjectCorrectly`
- **Frontend integration test** — update or add `apps/anchor-react/src/pages/PropertiesPage.test.tsx` (existing file):
  - Replace ERPNext MSW mock with a `/api/v1/properties` MSW handler returning the new DTO.
  - Assert page renders with the new field shapes.
  - Assert empty state copy updated.
  - Assert error state still works.
  - Assert refetch button still works.

Tests new this PR: ~8 Bridge + ~4 frontend.

#### 3.6 Verification before requesting council

- `dotnet build` succeeds for Sunfish.Bridge + its tests.
- `pnpm --filter anchor-react test` passes.
- `pnpm --filter anchor-react build` succeeds.
- Manual smoke: `pnpm dev` on anchor-react against a Bridge with seeded properties; PropertiesPage renders.
- Network panel verification (manual): only `/api/v1/properties` is called for PropertiesPage; no `/api/v1/erpnext/properties` fires.

#### 3.7 Pattern conformance + pre-auth gating

PR 1 sits at the intersection of **pattern-005** (DI extension addition — the `AuthenticatedTenantPolicy` registration + `MapPropertiesEndpoints` extension) and **candidate pattern-009** (Bridge endpoint + frontend rebind pair — first instance; the pattern earns ratification only after 3 clean shippings).

PR description must include:
```
@standing-pattern: pattern-005 (DI extension)
@candidate-pattern: pattern-009 (Bridge-endpoint + frontend rebind pair — first instance; not yet ratified)
```

Per the pre-authorization ruling §Step 3.9: deviations from spec get a `@deviation-from-spec:` line. Cohort 1 PR 1's deviations from prior patterns:
- New top-level Bridge route family (no precedent for `/api/v1/<cluster>` outside `cockpit/`) — but this is exactly what the roadmap §4 prescribes, so it is not a deviation from THIS hand-off.
- The cluster-endpoint pattern itself is novel; security-engineering SPOT-CHECK absorbs that risk.

If council returns BLOCKING, the pre-authorization shortcut is voided for PR 1 per §Step 4 ("Council returns BLOCKING — council can override pre-authorization"). CO clicks merge.

#### 3.8 Do NOT in this PR

- Do NOT add `/api/v1/leases` or `/api/v1/maintenance/*` endpoints — Cohort 1 PRs 2 + 3 own those.
- Do NOT mark `erpnext.ts` exports `@deprecated` — Cohort 1 PR 4 owns the deprecation pass.
- Do NOT delete the ERPNext route `/api/v1/erpnext/properties` from the Bridge passthrough — Cohort 4 (RB-12) owns the deletion per the roadmap §4 Cohort 4 + CO Q2 ruling ("keep for one milestone; delete after demo").
- Do NOT introduce a new state-management library, new error-handling component, or new loading-spinner pattern. Reuse existing.
- Do NOT touch `accelerators/bridge/Sunfish.Bridge/Cockpit/**`. New endpoint family lives at `accelerators/bridge/Sunfish.Bridge/Properties/**`.
- Do NOT change the TanStack query-key. Same shape across rebind preserves cache continuity.
- Do NOT add `EntityTag` to `Property` or to `IPropertyRepository`. That's W#64's job; if absent on main, leave as TODO.

---

### PR 2 — `/api/v1/leases` Bridge endpoint family + `LeasesPage` + `LeaseDetailPage` rebind (bundled)

**Estimated effort:** ~2h
**Scope:** new Bridge route family `/api/v1/leases`; new top-level frontend client; `useLeases.ts` + `useLease.ts` fetcher swap; `LeasesPage.tsx` + `LeaseDetailPage.tsx` type-shape adjustment; payments query left calling ERPNext (deferred to Cohort 2)
**Commit subject:** `feat(anchor-react,bridge): rebind LeasesPage + LeaseDetailPage to /api/v1/leases cluster endpoints (cohort 1 PR 2)`
**Branch:** `cob/anchor-react-rebind-leases`
**Depends on:** PR 1 merged (reuses `AuthenticatedTenantPolicy`)
**Pre-merge council:** NOT required (mechanical mirror of PR 1's pattern; same policy; no new auth surface). If anything deviates from PR 1's shape, escalate.

#### 3.9 Bridge endpoint family — `/api/v1/leases`

New file: `accelerators/bridge/Sunfish.Bridge/Leases/LeasesEndpoints.cs`. Two endpoints:

1. **`GET /` — list active leases for the authenticated tenant**
   - Tenant scoping via `ITenantContext` (same as PR 1).
   - Returns `LeaseListDto { leases: LeaseSummaryDto[] }`.
   - Filter: phase = `Active` by default. Optional `?phase=All` or `?phase=Expired` query param expands; default behavior matches existing LeasesPage UX.
   - Method signature:
     ```text
     internal static async Task<Ok<LeaseListDto>> HandleListLeasesAsync(
         ITenantContext tenantContext,
         ILeaseService leases,
         string? phase,
         CancellationToken ct)
     ```

2. **`GET /{id}` — lease detail**
   - Same tenant scoping.
   - `id` is the `LeaseId.Value` string.
   - Returns `LeaseDetailDto` or `NotFound`.
   - Method signature:
     ```text
     internal static async Task<Results<Ok<LeaseDetailDto>, NotFound>> HandleGetLeaseDetailAsync(
         string id,
         ITenantContext tenantContext,
         ILeaseService leases,
         CancellationToken ct)
     ```

3. **DTO shape:**
   ```text
   LeaseListDto:
     leases: LeaseSummaryDto[]

   LeaseSummaryDto:
     leaseId: string
     tenantDisplayName: string       // first tenant in Lease.Tenants; fallback "(no tenant)"
     propertyId: string?             // via PropertyUnit lookup; null if not resolvable
     propertyDisplayName: string?    // best-effort; null if not resolvable
     unitId: string?
     startDate: string               // ISO date "YYYY-MM-DD"
     endDate: string
     monthlyRent: number             // decimal serialized as number; matches existing erpnext.Lease.monthly_rent
     status: 'Active' | 'Expired' | 'Terminated'   // derived from LeasePhase

   LeaseDetailDto:
     ...all LeaseSummaryDto fields...
     securityDeposit: number?
     leaseTerm: string?             // "12 months", "month-to-month", etc.
     tenants: LeaseTenantDto[]      // full tenant list, not just first
     notes: string?

   LeaseTenantDto:
     partyId: string
     displayName: string
   ```

4. **Registration extension:**
   ```text
   public static IEndpointRouteBuilder MapLeasesEndpoints(this IEndpointRouteBuilder app)
   {
     var group = app.MapGroup("/api/v1/leases").RequireAuthorization(AuthenticatedTenantPolicyName);
     group.MapGet("/",     HandleListLeasesAsync).WithName("ListLeases");
     group.MapGet("/{id}", HandleGetLeaseDetailAsync).WithName("GetLeaseDetail");
     return app;
   }
   ```

   Wire into `Program.cs` next to `MapPropertiesEndpoints()`.

5. **Tenant scoping for lease queries:** `ILeaseService.ListAsync(ListLeasesQuery, ct)` (per the existing surface in `packages/blocks-leases/Services/ListLeasesQuery.cs`) does NOT include a TenantId filter in the query type itself — tenant scoping is via the repository's tenant-aware enumeration. **Verify the existing query path actually filters by tenant on the in-memory repo** before shipping; if not (likely the case in the current in-memory v1 implementation), file a halt note and add a `WhereTenantMatches(ITenantContext)` filter inline in the endpoint as a defensive measure. **See Halt H1 below.**

#### 3.10 Frontend client — `apps/anchor-react/src/api/leases.ts`

New file. Mirror of `properties.ts`:

```text
export interface LeaseSummary {
  leaseId: string
  tenantDisplayName: string
  propertyId: string | null
  propertyDisplayName: string | null
  unitId: string | null
  startDate: string
  endDate: string
  monthlyRent: number
  status: 'Active' | 'Expired' | 'Terminated'
}

export interface LeaseList {
  leases: LeaseSummary[]
}

export interface LeaseDetail extends LeaseSummary {
  securityDeposit: number | null
  leaseTerm: string | null
  tenants: LeaseTenant[]
  notes: string | null
}

export interface LeaseTenant {
  partyId: string
  displayName: string
}

export async function getLeases(phase?: 'Active' | 'Expired' | 'All'): Promise<LeaseList> {
  const url = phase ? `/api/v1/leases?phase=${encodeURIComponent(phase)}` : '/api/v1/leases'
  const resp = await fetch(url, { credentials: 'include' })
  if (!resp.ok) throw new Error(`Failed to load leases: ${resp.status} ${resp.statusText}`)
  return (await resp.json()) as LeaseList
}

export async function getLease(leaseId: string): Promise<LeaseDetail> {
  const resp = await fetch(`/api/v1/leases/${encodeURIComponent(leaseId)}`, { credentials: 'include' })
  if (resp.status === 404) throw new Error('Lease not found')
  if (!resp.ok) throw new Error(`Failed to load lease: ${resp.status} ${resp.statusText}`)
  return (await resp.json()) as LeaseDetail
}
```

#### 3.11 Hook rebind — `apps/anchor-react/src/hooks/useLeases.ts`

```text
import { useQuery } from '@tanstack/react-query'
import { getLeases, getLease } from '@/api/leases'    // changed from '@/api/erpnext'
import { getPayments } from '@/api/erpnext'           // KEEP — payments deferred to Cohort 2 RB-8
import { useCompanyStore } from '@/stores/companyStore'

export function useLeases() {
  const activeCompany = useCompanyStore((s) => s.activeCompany)
  return useQuery({
    queryKey: ['leases', activeCompany],              // unchanged
    queryFn: () => getLeases('Active').then(r => r.leases),  // unwrap to keep consumer shape
    retry: 2,
    retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10000),
  })
}

export function useLease(name: string) {
  const activeCompany = useCompanyStore((s) => s.activeCompany)
  return useQuery({
    queryKey: ['lease', name, activeCompany],
    queryFn: () => getLease(name),
    enabled: Boolean(name),
  })
}

// Keep usePayments() calling /api/v1/erpnext/payments — Cohort 2 RB-8 rebinds it.
export function usePayments() {
  const activeCompany = useCompanyStore((s) => s.activeCompany)
  return useQuery({
    queryKey: ['payments', activeCompany],
    queryFn: getPayments,
    retry: 2,
    retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 10000),
  })
}
```

**Important — preserve consumer shape:** `useLeases().data` was previously `Lease[]` (the array directly). The new endpoint returns `{ leases: LeaseSummary[] }`. To minimize churn in `LeasesPage.tsx`, unwrap in the queryFn: `queryFn: () => getLeases('Active').then(r => r.leases)`. This keeps `useLeases().data` shape as `LeaseSummary[] | undefined`. The page only needs to update its `import type` line + field names.

#### 3.12 Page rebind — `apps/anchor-react/src/pages/LeasesPage.tsx`

Changes:
1. `import type { Lease } from '@/api/erpnext'` → `import type { LeaseSummary } from '@/api/leases'`.
2. Field-name mapping inside the table + expiring-leases banner:
   - `l.name` → `l.leaseId`
   - `l.tenant` → `l.tenantDisplayName`
   - `l.property` → `l.propertyDisplayName ?? l.propertyId ?? '—'`
   - `l.unit` → `l.unitId ?? ''`
   - `l.start_date` → `l.startDate`
   - `l.end_date` → `l.endDate`
   - `l.monthly_rent` → `l.monthlyRent`
   - `l.status` → `l.status` (same union: `'Active' | 'Expired' | 'Terminated'`)
3. The `daysUntilExpiry()` helper reads `endDate` — works without change.
4. Empty-state copy: `"Create a Lease record in ERPNext."` → `"Add a lease in the cockpit to get started."`.
5. `LeaseStatusBadge` props: type is `LeaseSummary['status']` (same union; no change).

#### 3.13 Page rebind — `apps/anchor-react/src/pages/LeaseDetailPage.tsx`

Changes:
1. `useLease(name)` now returns `LeaseDetail` (DTO; same hook signature). Adjust field names:
   - `lease.tenant` → `lease.tenantDisplayName` (or `lease.tenants[0]?.displayName ?? '(no tenant)'`)
   - `lease.property` → `lease.propertyDisplayName ?? lease.propertyId ?? '—'`
   - `lease.unit` → `lease.unitId`
   - `lease.start_date` → `lease.startDate`
   - `lease.end_date` → `lease.endDate`
   - `lease.monthly_rent` → `lease.monthlyRent`
   - `lease.status` → `lease.status`
2. The payments section continues to call `usePayments()` (still ERPNext-backed). Add a small TODO comment near the payments section: `// TODO: payments will rebind in Cohort 2 RB-8 once blocks-financial-payments lands.`
3. `allPayments?.filter((p) => p.lease === lease.name)` — `p.lease` is still ERPNext-shape; `lease.name` was the ERPNext id. Replace with `lease.leaseId`: `allPayments?.filter((p) => p.lease === lease.leaseId)`. **Note:** this only works if the ERPNext payments endpoint returns payments keyed by the same ID format as the new lease ID. If the ID formats diverge (likely — ERPNext uses doc names like `LEASE-0001`, blocks-leases uses ULIDs), payments will appear empty until Cohort 2 RB-8 rebinds the payments endpoint. **Document this in the page as a known temporary regression** (banner: "Payment history will appear after the next migration step"); see Halt H5.

#### 3.14 Tests

- **Bridge endpoint integration tests** — new file `accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/Leases/LeasesEndpointsTests.cs`:
  - `ListLeases_DefaultsToActivePhase`
  - `ListLeases_PhaseExpired_ReturnsExpiredOnly`
  - `ListLeases_PhaseAll_ReturnsAllRows`
  - `ListLeases_UnauthenticatedCaller_Returns401`
  - `ListLeases_WrongTenant_ReturnsEmptyList`
  - `ListLeases_TenantWithNoLeases_ReturnsEmptyArray`
  - `GetLeaseDetail_KnownId_ReturnsFullDetail`
  - `GetLeaseDetail_UnknownId_Returns404`
  - `GetLeaseDetail_WrongTenant_Returns404` (not a 403 — tenant boundaries are opaque)
  - `GetLeaseDetail_LeaseWithMultipleTenants_PopulatesTenantsArray`
- **Frontend test updates:**
  - `apps/anchor-react/src/pages/LeasesPage.test.tsx` (existing): swap MSW mocks; assert new field renders; assert empty-state copy update.
  - `apps/anchor-react/src/pages/LeaseDetailPage.test.tsx` (NEW if absent): assert field rendering; assert payments banner appears (regression banner per §3.13).

Tests new this PR: ~10 Bridge + ~4 frontend.

#### 3.15 Verification

- `dotnet build` succeeds.
- All PR 1 tests still pass.
- New tests pass.
- Manual smoke: `pnpm dev` on anchor-react; LeasesPage + LeaseDetailPage render against Bridge with seed leases.
- Network panel verification: only `/api/v1/leases*` for the two pages; no `/api/v1/erpnext/leases*` fires.
- Payments section on LeaseDetailPage shows the banner; payment list may be empty (acceptable; documented).

#### 3.16 Pattern conformance

```
@standing-pattern: pattern-005 (DI extension reuse)
@candidate-pattern: pattern-009 (Bridge-endpoint + frontend rebind pair — second instance; needs one more)
```

#### 3.17 Do NOT in this PR

- Do NOT touch `usePayments()` or `getPayments()`. Deferred to Cohort 2 RB-8.
- Do NOT change the `useLeases()` consumer shape (keep returning `LeaseSummary[]`, not `LeaseList`). Unwrap in the queryFn.
- Do NOT delete the ERPNext route `/api/v1/erpnext/leases*` from Bridge passthrough. PR 4 marks `@deprecated`; Cohort 4 deletes.
- Do NOT add a "renew lease" or "edit lease" mutation. Read-only this PR.

---

### PR 3 — `MaintenancePage` rebind (create endpoint + frontend swap)

**Estimated effort:** ~1.5h
**Scope:** add `POST` create-work-order endpoint; rebind `MaintenancePage` to call cockpit work-order family (list + detail already shipped) + new create endpoint; replace `erpnext.ts.getMaintenanceTickets/createMaintenanceTicket/updateMaintenanceTicket`
**Commit subject:** `feat(anchor-react,bridge): rebind MaintenancePage to /api/v1/cockpit/work-orders (cohort 1 PR 3)`
**Branch:** `cob/anchor-react-rebind-maintenance`
**Depends on:** PR 2 merged (no technical dependency; sequential for review discipline)
**Pre-merge council:** **conditional** — see §3.18 below; the route placement decision affects this.

#### 3.18 Route placement decision — XO recommendation

Per the roadmap §4 Cohort 1 row RB-4, the original plan was to **reuse `/api/v1/cockpit/work-orders/*`** for the rebind. That family already exists (`WorkOrdersEndpoint.cs`) and serves GET / + GET /{id}. The plan also added a `POST /` for create.

**However:** `/api/v1/cockpit/work-orders/*` is under `/api/v1/cockpit/` and inherits `CockpitPolicy` (role ∈ {owner, spouse}). This is **more restrictive** than `AuthenticatedTenantPolicy`. Two consequences:

1. If the existing `MaintenancePage.tsx` is meant to be reachable by a `manager` or `contractor` role (which `AuthRoleGate` in the page suggests it is), the cockpit policy will block them.
2. Touching `accelerators/bridge/Sunfish.Bridge/Cockpit/**` puts the PR on the "always-needs-full-pipeline" list per the standing-patterns catalog — it would NOT be eligible for pre-authorization shortcut, and would require security-engineering + .NET architect review.

**XO recommendation:** add a **new top-level route family `/api/v1/maintenance/work-orders/*`** that mirrors the cockpit family's GET shape AND adds `POST /` for create. Use `AuthenticatedTenantPolicy`. This:
- Stays outside `cockpit/` (eligible for pre-authorization)
- Allows broader role access (matches `MaintenancePage`'s `AuthRoleGate allow={['owner', 'manager']}`)
- Keeps the cockpit family narrowly cockpit-scoped per its original design intent

**Halt condition H4:** if CO prefers the cockpit-extension approach, halt and re-author PR 3 + the security-engineering council fires + the pre-auth shortcut for PR 3 is voided. Decision needed at hand-off review time.

**This hand-off prescribes the XO-recommended non-cockpit-route variant.** Sections below assume that path.

#### 3.19 Bridge endpoint family — `/api/v1/maintenance/work-orders`

New file: `accelerators/bridge/Sunfish.Bridge/Maintenance/MaintenanceEndpoints.cs`. Three endpoints:

1. **`GET /` — list work orders for the authenticated tenant** (filters: `status`, `vendorId`, `from`, `to`, `page`, `pageSize` — mirror cockpit family for shape consistency)
2. **`GET /{id}` — work-order detail** (mirrors cockpit detail endpoint shape)
3. **`POST /` — create a work order** (NEW; this is the only meaningful net-new surface)

DTO shape: mirror the cockpit family's `WorkOrderListDto` / `WorkOrderSummary` / `WorkOrderDetailDto` shapes exactly. **Do not invent new field names.** This keeps the React client's type-shape interchangeable with the existing cockpit work-orders surface — which the Anchor cockpit (`apps/anchor-react/src/cockpit/work-orders/*`) consumes already.

For `POST /` create:

```text
CreateWorkOrderRequest:
  subject: string                  // maps to WorkOrder.Notes initial value or new Subject field if added
  propertyId: string?              // optional; falls through to tenant default if absent
  priority: 'Low' | 'Medium' | 'High' | 'Critical'   // maps to WorkOrder.Priority
  assignedVendorId: string?        // optional; null = unassigned
  description: string?

CreateWorkOrderResponse:
  workOrderId: string
  status: string                   // initial state (typically "Open" or "Pending")
  createdAt: string                // ISO timestamp
```

Method signature:
```text
internal static async Task<Results<Ok<CreateWorkOrderResponse>, BadRequest<ProblemDetails>>>
  HandleCreateWorkOrderAsync(
      CreateWorkOrderRequest request,
      ITenantContext tenantContext,
      IMaintenanceService maintenance,
      CancellationToken ct)
```

Validation:
- `subject` required, non-empty, ≤200 chars (return 400 with ProblemDetails on miss).
- `priority` must parse to `WorkOrderPriority` enum (or whatever blocks-maintenance uses).
- `propertyId` if provided must resolve to a property on this tenant (else 400).
- `assignedVendorId` if provided must resolve to a vendor on this tenant (else 400).

Implementation calls into `IMaintenanceService.CreateWorkOrderAsync(...)` (or whatever the existing surface is — verify before shipping; if absent, this is a gap that needs a small `blocks-maintenance` addition, which is itself out of cluster-endpoint scope — **see Halt H6**).

Registration extension:
```text
public static IEndpointRouteBuilder MapMaintenanceEndpoints(this IEndpointRouteBuilder app)
{
  var group = app.MapGroup("/api/v1/maintenance/work-orders").RequireAuthorization(AuthenticatedTenantPolicyName);
  group.MapGet("/",     HandleListWorkOrdersAsync).WithName("ListMaintenanceWorkOrders");
  group.MapGet("/{id}", HandleGetWorkOrderDetailAsync).WithName("GetMaintenanceWorkOrderDetail");
  group.MapPost("/",    HandleCreateWorkOrderAsync).WithName("CreateMaintenanceWorkOrder");
  return app;
}
```

Wire into `Program.cs` next to `MapLeasesEndpoints()`.

#### 3.20 Frontend client — `apps/anchor-react/src/api/maintenance.ts`

New file. Mirrors `cockpit/api.ts` work-orders section but at the new top-level URL:

```text
export interface MaintenanceWorkOrderSummary {
  workOrderId: string
  status: string
  vendorId: string
  scheduledDate: string
  completedDate: string | null
  appointmentDate: string | null
}

export interface MaintenanceWorkOrderList {
  items: MaintenanceWorkOrderSummary[]
  total: number
  page: number
  pageSize: number
}

// ... DetailDto mirroring CockpitWorkOrderDetail ...

export interface CreateWorkOrderInput {
  subject: string
  propertyId?: string
  priority: 'Low' | 'Medium' | 'High' | 'Critical'
  assignedVendorId?: string
  description?: string
}

export interface CreateWorkOrderResult {
  workOrderId: string
  status: string
  createdAt: string
}

export async function listMaintenanceWorkOrders(params: ListParams = {}): Promise<MaintenanceWorkOrderList> {
  // ... same query-string building as cockpit work-orders client ...
}

export async function getMaintenanceWorkOrderDetail(id: string): Promise<MaintenanceWorkOrderDetail> {
  // ...
}

export async function createMaintenanceWorkOrder(input: CreateWorkOrderInput): Promise<CreateWorkOrderResult> {
  const resp = await fetch('/api/v1/maintenance/work-orders', {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(input),
  })
  if (!resp.ok) {
    const detail = await resp.text().catch(() => resp.statusText)
    throw new Error(`Failed to create work order: ${resp.status} ${detail}`)
  }
  return (await resp.json()) as CreateWorkOrderResult
}

// Update mutation (status / cost / resolution) deferred to a follow-on PR;
// for Cohort 1 the page can show but not mutate beyond create.
```

#### 3.21 Page rebind — `apps/anchor-react/src/pages/MaintenancePage.tsx`

Changes:
1. Replace `getMaintenanceTickets / createMaintenanceTicket / updateMaintenanceTicket / MaintenanceTicket / CreateMaintenanceInput` imports from `@/api/erpnext` with the new equivalents from `@/api/maintenance`.
2. Field-name mapping:
   - `ticket.name` → `wo.workOrderId`
   - `ticket.subject` → `wo.subject` (if present in detail DTO) OR map to `wo.notes` if subject isn't surfaced; document the choice in the page
   - `ticket.property` → `wo.propertyId` (display the ID; resolve to display name via a property hook OR show the ID directly with a tooltip)
   - `ticket.status` → `wo.status` (same set: Open / In Progress / Resolved / Closed — verify against blocks-maintenance WorkOrderStatus enum; map if necessary)
   - `ticket.priority` → `wo.priority`
   - `ticket.assigned_to` → `wo.vendorId` (or vendor display name from detail)
   - `ticket.cost` → derived from `wo.totalCost` if surfaced; else hide column for v1
3. **Status update dropdown** — the current page's `updateMaintenanceTicket({ Status })` mutation. Cohort 1 PR 3 ships create-only — **update is deferred**. Replace the dropdown with a read-only badge for now; comment out (do not delete) the mutation hookup with `// TODO: update endpoint ships in cohort 1 follow-on or cohort 2.` This is a known temporary functionality regression; document on the page banner: "Status updates will return after the next migration step."
4. Empty state copy: `"No maintenance tickets. Create one in ERPNext."` → `"No work orders yet. Click + New to create one."`.

**Functionality regression note** — disabling status updates is intentional and documented. The alternative is shipping an `UpdateWorkOrderRequest` endpoint in the same PR, which expands scope and reduces pattern conformance. CO can ratify the regression at hand-off review.

#### 3.22 Tests

- **Bridge endpoint integration tests** — new file `accelerators/bridge/tests/Sunfish.Bridge.Tests.Unit/Maintenance/MaintenanceEndpointsTests.cs`:
  - `ListWorkOrders_AuthenticatedTenant_ReturnsRows`
  - `ListWorkOrders_UnauthenticatedCaller_Returns401`
  - `GetWorkOrderDetail_KnownId_ReturnsDetail`
  - `GetWorkOrderDetail_UnknownId_Returns404`
  - `CreateWorkOrder_ValidRequest_ReturnsCreatedId`
  - `CreateWorkOrder_MissingSubject_Returns400`
  - `CreateWorkOrder_OverlongSubject_Returns400`
  - `CreateWorkOrder_InvalidPriority_Returns400`
  - `CreateWorkOrder_UnknownVendor_Returns400`
  - `CreateWorkOrder_PropertyOnDifferentTenant_Returns400`
  - `CreateWorkOrder_TenantScoping_AssignsToCallerTenant` (cross-tenant safety)
- **Frontend test updates:**
  - Update or add `apps/anchor-react/src/pages/MaintenancePage.test.tsx`: swap MSW mocks; assert list rendering with new field shapes; assert create form posts to new endpoint; assert status update dropdown is read-only.

Tests new this PR: ~11 Bridge + ~5 frontend.

#### 3.23 Verification

- `dotnet build` succeeds.
- All PR 1 + PR 2 tests still pass.
- New tests pass.
- Manual smoke: page renders + create flow round-trips.
- Network panel: only `/api/v1/maintenance/work-orders*` for the page; no `/api/v1/erpnext/maintenance*` fires.

#### 3.24 Pattern conformance

```
@standing-pattern: pattern-005 (DI extension reuse)
@candidate-pattern: pattern-009 (Bridge-endpoint + frontend rebind pair — third instance; this is the qualifying shipping IF pattern-009 is proposed for ratification after Cohort 1)
@deviation-from-spec: status-update mutation removed from MaintenancePage (read-only fallback); per §3.21 — CO ratified at hand-off review
```

If pattern-009 is ratified as a result of Cohort 1's three clean shippings, subsequent rebind cohorts can skip security-engineering SPOT-CHECK on the cluster-endpoint shape.

#### 3.25 Do NOT in this PR

- Do NOT extend the cockpit work-orders family (don't add `POST /` under `/api/v1/cockpit/`). Stay outside cockpit per XO recommendation; if CO overrides at H4, re-author.
- Do NOT add `UpdateWorkOrderRequest` or any mutation beyond create. Deferred.
- Do NOT touch vendor or appointment endpoints. Out of scope.
- Do NOT change the AuthRoleGate logic in MaintenancePage. Server-side enforcement is sufficient under `AuthenticatedTenantPolicy`; the page's `AuthRoleGate` remains as a defensive UI gate.

---

### PR 4 — Cohort 1 cleanup, smoke test, docs, ledger flip

**Estimated effort:** ~1.5h
**Scope:** mark four `erpnext.ts` exports `@deprecated` with dev-mode console warning; author `apps/docs/anchor/anchor-react-rebind.md`; add Playwright CDP E2E smoke test asserting all four pages render with NO ERPNext network calls; flip W#74 ledger row from `building` → `built`
**Commit subject:** `chore(anchor-react,docs): cohort 1 cleanup — deprecate erpnext.ts entries, add smoke test, flip W#74 (cohort 1 PR 4)`
**Branch:** `cob/anchor-react-rebind-cohort-1-cleanup`
**Depends on:** PR 3 merged
**Pre-merge council:** NOT required (cleanup + docs + ledger flip). **CO sees this PR regardless of pre-authorization** — ledger-flip PR per pre-auth ruling §Step 4.

#### 3.26 ERPNext deprecation marks

Edit `apps/anchor-react/src/api/erpnext.ts`. For each of the four exports consumed by Cohort 1:

- `getProperties()`
- `getLeases()`
- `getLease(name)`
- `getMaintenanceTickets()`, `createMaintenanceTicket()`, `updateMaintenanceTicket(name, payload)`

Add:

```text
/**
 * @deprecated Cohort 1 (W#74) rebound this surface to /api/v1/properties.
 * Use `import { getProperties } from '@/api/properties'` instead.
 * This ERPNext-backed entry will be removed in Cohort 4 (RB-12) per the
 * Anchor React rebind roadmap. ERPNext remains the migration source
 * (Wave/Rentler import), but is no longer the runtime data plane.
 */
export async function getProperties(): Promise<Property[]> {
  if (import.meta.env.DEV) {
    console.warn(
      '[anchor-react] getProperties() (erpnext) is deprecated. ' +
      'Use getProperties from @/api/properties. Will be removed in Cohort 4.'
    )
  }
  // ... existing impl unchanged ...
}
```

Same shape for the other five exports. Console warning only in DEV (gates on `import.meta.env.DEV` to avoid noisy logs in Tauri production builds).

**Do NOT delete the exports or their type definitions.** ERPNext routes remain available for Cohorts 2 + 3 + migration orchestrator per CO Q2.

**Do NOT modify the Bridge `/api/v1/erpnext/*` passthrough routes.** Those stay live through Cohort 4 per CO Q2.

#### 3.27 E2E smoke test

Add a Playwright CDP E2E test (per `feedback_anchor_smoke_test_playwright_cdp`):

- New file under the appropriate E2E test root for Anchor (likely `apps/anchor-tauri/tests/e2e/` or wherever existing Playwright CDP smoke tests live — verify at PR time).
- Test scenario: `cohort-1-rebind-no-erpnext.spec.ts`:

  ```text
  Test name: "Cohort 1 pages render without ERPNext"
  Setup:
    - Bring up Bridge with seeded properties, leases, work orders (no ERPNext running)
    - Bring up Anchor (Tauri shell) pointing at the Bridge
    - Open Playwright via CDP attached to the Tauri WebView
  Steps for each page:
    1. Navigate to /properties; wait for content
    2. Navigate to /leases; wait for content
    3. Navigate to /leases/{seed-id}; wait for content
    4. Navigate to /maintenance; wait for content
  Assertions per page:
    - At least one card / row / detail-field renders
    - The page does NOT show its error state
    - Network log captured via CDP shows ZERO requests to /api/v1/erpnext/* originating from the page route
      EXCEPTION: LeaseDetailPage's usePayments() still calls /api/v1/erpnext/payments — this is the
      known temporary regression; the test asserts the payments network call is the ONLY ERPNext
      request, and is allowlisted.
  Pass criteria: all four pages render + network assertion passes
  ```

- If Playwright CDP infrastructure isn't yet wired for Anchor (verify at PR time — recent feedback memory suggests it exists but cohort 1 may be the first cohort to exercise it), file Halt H7.

#### 3.28 `apps/docs/anchor/anchor-react-rebind.md`

New file. Running log of the rebind cohort progression. Structure:

```text
# Anchor React → blocks-* Rebind Status

This page tracks the rebind progress per the Anchor React Bridge Rebind
Roadmap (`icm/02_architecture/anchor-react-bridge-rebind-roadmap.md`).
Anchor React's data plane is shifting from `/api/v1/erpnext/*` to
`/api/v1/<cluster>/*` cluster endpoints served by Bridge. The shift
reflects ADR 0088 Path II: Anchor's native domain is `blocks-*`, not
ERPNext at runtime.

## Page-by-page status

| Page | Status | Bridge endpoint | Workstream | PR |
|---|---|---|---|---|
| PropertiesPage | rebound | `/api/v1/properties` | W#74 (Cohort 1) | #NNN |
| LeasesPage | rebound | `/api/v1/leases` | W#74 (Cohort 1) | #NNN |
| LeaseDetailPage | rebound (payments deferred) | `/api/v1/leases/{id}` | W#74 (Cohort 1) | #NNN |
| MaintenancePage | rebound (create only; update deferred) | `/api/v1/maintenance/work-orders/*` | W#74 (Cohort 1) | #NNN |
| AccountingPage | pending | TBD | Cohort 2 | — |
| RentCollectionPage | pending | TBD | Cohort 2 | — |
| PLReport | pending | `/api/v1/financial/reports/pl-by-property` | Cohort 3 | — |
| RentRoll | pending | `/api/v1/financial/reports/rent-roll` | Cohort 3 | — |
| (cleanup) | pending | — | Cohort 4 | — |

## ERPNext deprecation timeline

| Milestone | Routes deprecated | Routes deleted |
|---|---|---|
| Cohort 1 (this PR) | `/api/v1/erpnext/properties`, `/api/v1/erpnext/leases*`, `/api/v1/erpnext/maintenance*` (marked `@deprecated` in `erpnext.ts`) | None |
| Cohort 2 | + AccountingPage routes (`/api/v1/erpnext/accounting/*`, `/api/v1/erpnext/payments`) | None |
| Cohort 3 | + report routes (`/api/v1/erpnext/reports/*`) | None |
| Cohort 4 | All Cohort 1-3 marks remain | All `/api/v1/erpnext/*` routes deleted from Bridge passthrough; `erpnext.ts` deleted |

Per CO ratification 2026-05-17T14-30Z: ERPNext routes are kept for one milestone after deprecation
(to support migration orchestrator + give time for parallel cohort work to land), then deleted wholesale
at Cohort 4.

## Cohort 1 known temporary regressions

These were intentional in Cohort 1 and will resolve in later cohorts:

1. **LeaseDetailPage payment history** — `usePayments()` still calls `/api/v1/erpnext/payments`. Payment IDs in the new lease shape don't match the legacy IDs, so the filter is empty. Resolves when Cohort 2 RB-8 rebinds the payments endpoint to `blocks-financial-ar` + `blocks-financial-payments`.
2. **MaintenancePage status updates** — the status dropdown is currently read-only. The Cohort 1 PR 3 endpoint family only ships create; update mutations follow in a Cohort 1 addendum or Cohort 2 work-orders writeback PR.

## Cohort acceptance gates

See the per-cohort acceptance gates in `icm/02_architecture/anchor-react-bridge-rebind-roadmap.md` §4.
```

(Length target ~3-5 KB for v1; expand as cohorts ship.)

Add the new file to `apps/docs/anchor/toc.yml` (or whatever the docs site's TOC structure is — verify at PR time). This piece matches **pattern-006**.

#### 3.29 Ledger flip

Per `feedback_never_add_workstream_rows_directly_to_ledger`:

1. Edit the source file `icm/_state/workstreams/W74-anchor-react-rebind-cohort-1.md`. Update the `State:` line from `building` to `built`. Add PR refs (#NNN, #NNN, #NNN, #NNN) in the body.
2. Re-render the ledger by running `tools/icm/render-ledger.py` (or whatever the canonical render command is on main).
3. Both files (source + rendered ledger) ship in PR 4.

This is **pattern-007** territory; eligible for the standing-pattern shortcut. PR description carries `@standing-pattern: pattern-007 + pattern-006 + pattern-008 (erpnext.ts touch-up)`.

#### 3.30 Tests

- E2E smoke test (per §3.27).
- No new unit tests (cleanup + docs).
- `pnpm --filter anchor-react build` + `pnpm --filter anchor-react test` continue to pass.
- `dotnet build` succeeds.

#### 3.31 Verification

- All Cohort 1 PRs (1, 2, 3) have merged.
- E2E smoke test passes locally.
- The four pages render with ERPNext NOT running (modulo the payments banner on LeaseDetailPage and the read-only status badge on MaintenancePage).
- The new docs page is reachable at `apps/docs/anchor/anchor-react-rebind.md` and renders correctly in the docs site preview.
- W#74 ledger row reads `built`.

#### 3.32 Pattern conformance

```
@standing-pattern: pattern-006 (new docs page) + pattern-007 (ledger flip) + pattern-008 (erpnext.ts deprecation touch-up)
```

If CO ratifies pattern-009 as part of accepting Cohort 1, add to PR description:
```
@pattern-009-qualifying: Cohort 1 is the third clean shipping of the Bridge-endpoint + frontend rebind pair pattern (PRs 1, 2, 3 — three instances) — proposes pattern-009 for catalog ratification at CO review of PR 4
```

#### 3.33 Do NOT in this PR

- Do NOT delete `erpnext.ts` or any ERPNext route. Mark `@deprecated` only.
- Do NOT delete the Bridge `/api/v1/erpnext/*` passthrough.
- Do NOT add Cohort 2 or Cohort 3 endpoints. Out of scope.
- Do NOT rebind `usePayments()`. Cohort 2 RB-8.
- Do NOT add the missing MaintenancePage update endpoint. Cohort 1 addendum if needed; Cohort 2 otherwise.
- Do NOT modify any ADR or workstream file outside the W#74 source row and the ledger render output.

---

## 4. Cross-cluster integration

| Frontend page | Frontend hook | Frontend client | Bridge endpoint | Bridge handler | Repository / service |
|---|---|---|---|---|---|
| `PropertiesPage.tsx` | `useProperties()` | `apps/anchor-react/src/api/properties.ts` | `GET /api/v1/properties` | `Sunfish.Bridge.Properties.PropertiesEndpoints.HandleListPropertiesAsync` | `Sunfish.Blocks.Properties.Services.IPropertyRepository.ListByTenantAsync` (+ optional `ListByEntityTagAsync` when W#64 ships) |
| `LeasesPage.tsx` | `useLeases()` | `apps/anchor-react/src/api/leases.ts` | `GET /api/v1/leases?phase=Active` | `Sunfish.Bridge.Leases.LeasesEndpoints.HandleListLeasesAsync` | `Sunfish.Blocks.Leases.Services.ILeaseService.ListAsync(ListLeasesQuery { Phase = Active })` |
| `LeaseDetailPage.tsx` | `useLease(id)` | `apps/anchor-react/src/api/leases.ts` | `GET /api/v1/leases/{id}` | `Sunfish.Bridge.Leases.LeasesEndpoints.HandleGetLeaseDetailAsync` | `Sunfish.Blocks.Leases.Services.ILeaseService` (single-record fetch — verify the existing surface includes a `GetByIdAsync(LeaseId, ct)` method; if absent, add inline via in-memory enumeration as a small Bridge-side helper) |
| `MaintenancePage.tsx` (list) | TanStack query | `apps/anchor-react/src/api/maintenance.ts` | `GET /api/v1/maintenance/work-orders` | `Sunfish.Bridge.Maintenance.MaintenanceEndpoints.HandleListWorkOrdersAsync` | `Sunfish.Blocks.Maintenance.Services.IMaintenanceService.ListWorkOrdersAsync` |
| `MaintenancePage.tsx` (detail click-through, if wired in this PR) | TanStack query | `apps/anchor-react/src/api/maintenance.ts` | `GET /api/v1/maintenance/work-orders/{id}` | `Sunfish.Bridge.Maintenance.MaintenanceEndpoints.HandleGetWorkOrderDetailAsync` | `Sunfish.Blocks.Maintenance.Services.IMaintenanceService.GetWorkOrderAsync(WorkOrderId, ct)` |
| `MaintenancePage.tsx` (create) | TanStack mutation | `apps/anchor-react/src/api/maintenance.ts` | `POST /api/v1/maintenance/work-orders` | `Sunfish.Bridge.Maintenance.MaintenanceEndpoints.HandleCreateWorkOrderAsync` | `Sunfish.Blocks.Maintenance.Services.IMaintenanceService.CreateWorkOrderAsync(...)` (verify exact method name; if absent, see Halt H6) |

All Bridge handlers resolve `TenantId` via `ITenantContext` (existing convention from cockpit endpoints); no tenant filter is accepted as a query parameter. EntityTag (W#64) is server-resolved analogously.

---

## 5. Idempotency-key catalog

**Not applicable.** Cohort 1's surface is read-side plus one create endpoint:

- Read endpoints have no idempotency keys (GETs are naturally idempotent at the protocol level).
- `POST /api/v1/maintenance/work-orders` is **not** marked idempotent. A double-submit creates two work orders. This matches the existing cockpit and ERPNext behavior and is acceptable for v1. If demand emerges (e.g., users double-clicking create), a follow-on can add an `Idempotency-Key` header per the standard pattern.

---

## 6. Dependencies + sequence

**Critical path:** PR 1 → PR 2 → PR 3 → PR 4. Sequential, primarily for CO review-load smoothing under pre-authorization (CO sees PR 4 even if pre-auth granted; PR 1 council results inform PR 2 + PR 3 risk).

Within the Bridge codebase:
- PR 1 adds `AuthenticatedTenantPolicy` + `MapPropertiesEndpoints`. PRs 2 + 3 depend on the policy being registered.
- All three Bridge endpoint files are independent at the C# level; no inter-file coupling.

Within the frontend:
- PR 1 adds `@/api/properties`. PR 2 adds `@/api/leases`. PR 3 adds `@/api/maintenance`. None depends on the others.
- Hooks `useProperties.ts` + `useLeases.ts` are each touched once. The `usePayments()` hook is untouched in Cohort 1.

**Parallel work possible:** PRs 1, 2, 3 could technically ship in parallel (no inter-PR conflicts on the codebase). XO recommends sequential for review discipline; CO may grant parallel at hand-off review.

**External gates (non-blocking but noted):**
- W#64 EntityTag shipment — Cohort 1 ships with the server-side filter as a no-op until W#64 lands. Touch-up PR after W#64 wires the filter.
- Future MaintenancePage update endpoint — Cohort 1 addendum or Cohort 2 work-orders writeback. Not a Cohort 1 blocker.

---

## 7. License posture

**MIT clean-room.** All frontend code is original; the rebind reads from existing Sunfish surfaces (Bridge endpoints, blocks-* repositories), and no FOSS source was studied for this rebind.

Existing license notes:
- The cockpit-endpoint pattern that Cohort 1 lifts is also MIT (internal Sunfish code).
- The Tauri shell's auth-flow assumption (relative URLs + injected base) is canonical Tauri WebView usage; no third-party code borrowed.
- No NOTICE.md additions required.

---

## 8. Test plan summary

| PR | New Bridge tests | New frontend tests | Total |
|---|---|---|---|
| PR 1 (properties) | ~8 | ~4 | ~12 |
| PR 2 (leases) | ~10 | ~4 | ~14 |
| PR 3 (maintenance) | ~11 | ~5 | ~16 |
| PR 4 (cleanup + smoke) | E2E smoke (1 scenario, ~10 assertions) | — | ~1 (heavy) |
| **Total** | **~29 unit/integration + 1 E2E** | **~13** | **~43** |

### Cohort-level acceptance (PASS gate at end of PR 4)

**A1.** `dotnet build` succeeds for `Sunfish.Bridge` + every test project.

**A2.** `pnpm --filter anchor-react test` passes all PR 1 + PR 2 + PR 3 frontend tests.

**A3.** `pnpm --filter anchor-react build` succeeds (Vite production bundle generates).

**A4.** E2E smoke test (per §3.27) passes against a seeded Bridge with ERPNext **NOT running**.

**A5.** Manual network-panel verification: each of the four pages renders without any `/api/v1/erpnext/*` network call originating from the page route (excepting the LeaseDetailPage payments banner allowlisted).

**A6.** ERPNext routes still resolve at the Bridge level (passthrough not deleted). Spot-check: `curl /api/v1/erpnext/properties` still 200's. Cohort 4 deletes; Cohort 1 only deprecates.

**A7.** `apps/docs/anchor/anchor-react-rebind.md` renders in the docs site preview; TOC entry visible.

**A8.** Workstream W#74 ledger row reads `built`; PR refs #N, #N, #N, #N captured.

**A9.** `coordination/inbox/cob-status-2026-05-XXTHH-MMZ-w74-cohort-1-built.md` beacon dropped.

**A10.** No `@deviation-from-spec:` flag landed without CO acknowledgement.

---

## 9. Halt conditions (cob-question-* beacons)

### H1. `ILeaseService.ListAsync(ListLeasesQuery)` does not filter by tenant in the in-memory v1 implementation

**Symptom:** Bridge endpoint test `ListLeases_WrongTenant_ReturnsEmptyList` fails because the service surface returns rows from another tenant.

**Mitigation:** Add a defensive `WhereTenantMatches(ITenantContext)` filter inline in the endpoint, post-enumeration. Document in the handler with a TODO referencing the cluster surface gap. Optionally file a tiny follow-on hand-off to push tenant-scoping into `ListLeasesQuery` properly.

**Halt:** if the defensive in-endpoint filter cannot be applied because tenant identity isn't carried on `Lease`, **STOP** and file `cob-question-2026-05-XXTHH-MMZ-w74-lease-tenant-scoping.md`. XO must rule on whether to amend `blocks-leases` (out-of-scope for Cohort 1) or ship with a known caveat.

### H2. Tauri shell Bridge base-URL wiring unclear

**Symptom:** Frontend uses relative URLs (`/api/v1/properties`), but the Tauri shell doesn't proxy them to the Bridge process; manual smoke test fails with network errors that aren't 4xx/5xx (typically `ERR_CONNECTION_REFUSED` or similar).

**Mitigation:** Verify pre-build §2.3. If the shell's wiring is `tauri.conf.json` allowlist + Tauri runs the Bridge as a sidecar, the relative URL approach works. If not, the frontend may need `VITE_BRIDGE_BASE_URL` injection.

**Halt:** if Tauri-shell + Bridge wiring is genuinely undefined, **STOP** before PR 1's manual smoke step + file `cob-question-2026-05-XXTHH-MMZ-w74-tauri-bridge-baseurl.md`. XO must rule on the auth-flow shape (likely a small ADR amendment to 0088 or a runbook entry).

### H3. `RentRoll.tsx` already calls `/api/v1/reports/rent-roll` (NOT `/api/v1/erpnext/*`)

**Symptom:** Cohort 1 is meant to touch four pages; the audit confirms `RentRoll.tsx` already calls `/api/v1/reports/rent-roll` via `apps/anchor-react/src/api/erpnext.ts → getRentRoll()`. The endpoint is at `/api/v1/reports/rent-roll` (NOT under `/erpnext/`). Two possibilities:
   - The endpoint is already a non-ERPNext Bridge route → no rebind needed.
   - The endpoint is an `/api/v1/erpnext/*` passthrough route renamed to `/api/v1/reports/*` for display — the underlying server is still ERPNext.

**Mitigation:** Cohort 1 does NOT include `RentRoll.tsx`. Verify what `/api/v1/reports/rent-roll` resolves to on the Bridge today. If it's already cluster-backed → mark in the docs running log as already-rebound. If it's an ERPNext passthrough → leave for Cohort 3 (RB-10).

**Halt:** no halt; just verify and document the actual state in `apps/docs/anchor/anchor-react-rebind.md` per §3.28.

### H4. Cockpit route reuse vs. new top-level family for `MaintenancePage` (PR 3)

**Symptom:** XO recommends a new top-level `/api/v1/maintenance/work-orders/*` family (see §3.18). CO might prefer reusing the cockpit family.

**Mitigation:** This hand-off prescribes the XO-recommended new top-level family.

**Halt:** if CO rules at hand-off review that the cockpit reuse is preferred, halt and re-author PR 3 with:
- POST `/` added to `WorkOrdersEndpoint.cs` under `/api/v1/cockpit/work-orders/`
- `AuthRoleGate allow={['owner', 'spouse']}` enforced server-side (cockpit policy)
- Pre-authorization shortcut voided for PR 3 (cockpit touches require full pipeline per standing-patterns catalog)
- security-engineering + .NET architect council reviews fire

Drop a `cob-question-2026-05-XXTHH-MMZ-w74-pr3-route-placement.md` beacon if this decision isn't resolved at hand-off review.

### H5. Payments ID format divergence on `LeaseDetailPage`

**Symptom:** `allPayments?.filter((p) => p.lease === lease.leaseId)` returns empty because ERPNext payments are keyed by ERPNext-style IDs and the new lease IDs are ULIDs.

**Mitigation:** Document on the page as a known temporary regression; show a "Payment history will appear after the next migration step" banner. Resolves in Cohort 2 RB-8.

**Halt:** no halt; documented.

### H6. `IMaintenanceService.CreateWorkOrderAsync(...)` does not exist on the existing service surface

**Symptom:** Compiling PR 3's create endpoint fails because the method isn't on `IMaintenanceService`.

**Mitigation:** This is a small `blocks-maintenance` cluster surface gap. Two options:
1. Add the method inline in PR 3 (one-line interface + impl extension). Acceptable if the impl is mechanical (new ULID, set initial status to `Open`, persist via repository).
2. File a tiny prerequisite PR to `blocks-maintenance` that ships `CreateWorkOrderAsync` (~30 LOC) before PR 3.

XO recommends option 1 if the addition is mechanical; option 2 if the create logic needs validation, event emission, or audit hookup beyond pure persistence.

**Halt:** if the create logic turns out to need substantive blocks-maintenance work (audit, events, validators), **STOP** + file `cob-question-2026-05-XXTHH-MMZ-w74-pr3-maintenance-create.md`. XO must rule on scope expansion vs. cluster-side hand-off split.

### H7. Playwright CDP infrastructure for Anchor isn't yet wired

**Symptom:** PR 4's E2E smoke test can't run because the test harness for Anchor (Tauri + Playwright CDP) isn't yet set up.

**Mitigation:** Skip the smoke test in PR 4 if blocked. Document the gap in the docs running log. File a follow-on workstream to wire CDP smoke testing.

**Halt:** the smoke test is a Cohort 4 acceptance criterion per the roadmap; for Cohort 1, the smoke test is **preferred but not strictly required** to flip the ledger row to `built`. If CDP isn't wired, ship PR 4 without the smoke test scenario; replace it with a manual-test checklist in `apps/docs/anchor/anchor-react-rebind.md`. File `cob-question-2026-05-XXTHH-MMZ-w74-cdp-smoke-gap.md` so XO can route a follow-on workstream.

### H8. ERPNext route deprecation breaks any non-Cohort-1 page

**Symptom:** After PR 4 lands the `@deprecated` JSDoc + dev-console-warning, audit-check by running `pnpm --filter anchor-react build` shows a wave of warnings or (more concerning) the build fails because TypeScript treats `@deprecated` as an error under strict config.

**Mitigation:**
- Verify `tsconfig.json` strict mode treats `@deprecated` as a warning, not an error. (Standard TS behavior: deprecated decorator is a `tsserver` IDE hint only, not a compile error.)
- Audit non-Cohort-1 page imports of the four deprecated exports. Expected: `getProperties` used only by `useProperties.ts` (rebound), `getLeases`/`getLease` used by `useLeases.ts` (rebound), `getMaintenanceTickets`/`createMaintenanceTicket`/`updateMaintenanceTicket` used by `MaintenancePage.tsx` (rebound).
- If any other page or component imports the deprecated exports (e.g., a test helper, a story, a feature-flag config), update or annotate as part of PR 4.

**Halt:** if a non-Cohort-1 consumer is found that has a legitimate use of one of the deprecated exports, **STOP** + file `cob-question-2026-05-XXTHH-MMZ-w74-erpnext-deprecation-consumer.md`. XO must rule on whether to defer the deprecation for that export or unblock by rebinding the consumer.

### H9. EntityTag server-resolution mechanism unclear

**Symptom:** Pre-build §2.2 indicates `EntityTag` was specified by W#64 but isn't on main yet. Per CO Q1, the EntityTag filter is server-derived from tenant context — but the *mechanism* by which the server derives it (claims, cookie, tenant-bound service) isn't specified in the W#64 hand-off either (it's a property-level field, not a tenant-level state).

**Mitigation:** Cohort 1 ships with EntityTag filtering as a **no-op** (TODO). The endpoint accepts no `?entityTag=` param; the server applies no filter beyond tenant scoping. When W#64 ships AND the EntityTag-resolution-from-context shape is specified (likely in a small ADR amendment or a Bridge runbook entry), a touch-up PR wires it.

**Halt:** no halt for Cohort 1. If CO at hand-off review insists EntityTag filtering MUST be live for Cohort 1, file `cob-question-2026-05-XXTHH-MMZ-w74-entitytag-resolution-shape.md` requesting XO to author the resolution spec.

---

## 10. PASS gate (end-state for declaring W#74 `built`)

The hand-off ships when ALL of the following are true:

1. **PRs 1 + 2 + 3 + 4 merged to `main`** in sequence.
2. **Bridge endpoint integration tests pass** (acceptance A1).
3. **Frontend tests pass** (A2 + A3).
4. **E2E smoke test passes** OR (per H7) a manual-test checklist is published with PR 4 and CO accepts the manual-test substitute.
5. **Network-panel verification:** each of the four pages renders against a Bridge with ERPNext stopped, with only the LeaseDetailPage's `usePayments()` call allowlisted as a known temporary regression.
6. **ERPNext passthrough still works** at the Bridge level — Cohort 4 deletes; Cohort 1 only deprecates.
7. **`apps/docs/anchor/anchor-react-rebind.md` published** + linked in the docs TOC.
8. **Workstream W#74 ledger row reads `built`** with PR refs.
9. **`cob-status-2026-05-XXTHH-MMZ-w74-cohort-1-built.md`** beacon dropped to `coordination/inbox/`.
10. **No outstanding `@deviation-from-spec:` flags** without CO acknowledgement.
11. **Pattern-009 (rebind pair) catalog candidacy** — if XO observed three clean shippings (PRs 1, 2, 3), the W#74 status beacon includes a section proposing pattern-009 for catalog ratification at CO's next standing-patterns review.

When the PASS gate is met, the next cohort hand-offs can proceed:

- `anchor-react-rebind-cohort-2-stage06-handoff.md` — AccountingPage + RentCollectionPage (financial cluster pages; depends on `blocks-financial-payments` hand-off having landed or being concurrent)
- `anchor-react-rebind-cohort-3-stage06-handoff.md` — PLReport + RentRoll (reports cluster; depends on `blocks-reports` hand-off)
- `anchor-react-rebind-cohort-4-stage06-handoff.md` — cleanup pass + ERPNext route deletion

---

## 11. Cited-symbol verification

**Existing on origin/main (verified 2026-05-17):**

- `apps/anchor-react/src/pages/PropertiesPage.tsx` ✓
- `apps/anchor-react/src/pages/LeasesPage.tsx` ✓
- `apps/anchor-react/src/pages/LeaseDetailPage.tsx` ✓
- `apps/anchor-react/src/pages/MaintenancePage.tsx` ✓
- `apps/anchor-react/src/api/erpnext.ts` (target of deprecation) ✓
- `apps/anchor-react/src/cockpit/api.ts` (client-side template) ✓
- `apps/anchor-react/src/hooks/useProperties.ts` ✓
- `apps/anchor-react/src/hooks/useLeases.ts` ✓
- `apps/anchor-react/src/stores/companyStore.ts` ✓
- `accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitEndpoints.cs` (Bridge template) ✓
- `accelerators/bridge/Sunfish.Bridge/Cockpit/PropertyDetailEndpoint.cs` ✓
- `accelerators/bridge/Sunfish.Bridge/Cockpit/WorkOrdersEndpoint.cs` ✓
- `accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitPermissions.cs` ✓
- `packages/blocks-properties/Services/IPropertyRepository.cs` ✓
- `packages/blocks-properties/Services/InMemoryPropertyRepository.cs` ✓
- `packages/blocks-leases/Services/ILeaseService.cs` ✓
- `packages/blocks-leases/Services/ListLeasesQuery.cs` ✓
- `packages/foundation/Authorization/ITenantContext.cs` ✓
- `packages/foundation-multitenancy/ITenantContext.cs` ✓
- `icm/02_architecture/anchor-react-bridge-rebind-roadmap.md` ✓
- `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md` (Path II) ✓
- `_shared/engineering/standing-approved-patterns.md` (pattern-005, pattern-006, pattern-007, pattern-008) ✓
- `_shared/engineering/xo-ruling-workstream-pre-authorization.md` (Option B + pre-auth flow) ✓
- `icm/_state/handoffs/blocks-financial-ar-stage06-handoff.md` (canonical hand-off template) ✓

**Existing — referenced as gate / context (cited but not depended on by PR-author work):**

- `icm/_state/handoffs/blocks-properties-entity-tag-stage06-handoff.md` (W#64 — not yet built; EntityTag path is a TODO until it ships) ✓ (handoff exists; cluster surface not yet on main per verification)
- `feedback_anchor_smoke_test_playwright_cdp` (in `~/.claude/projects/.../memory/` — Playwright CDP smoke pattern) ✓
- `feedback_never_add_workstream_rows_directly_to_ledger` ✓
- `feedback_worktree_base_main_not_gitbutler` ✓

**Introduced by this hand-off (across PRs 1-4):**

- **PR 1:**
  - New file: `accelerators/bridge/Sunfish.Bridge/Authorization/AuthenticatedTenantPolicy.cs` (or extension to existing authorization config)
  - New file: `accelerators/bridge/Sunfish.Bridge/Properties/PropertiesEndpoints.cs`
  - New file: `apps/anchor-react/src/api/properties.ts`
  - Modified: `apps/anchor-react/src/hooks/useProperties.ts`
  - Modified: `apps/anchor-react/src/pages/PropertiesPage.tsx`
  - Modified: `apps/anchor-react/src/pages/PropertiesPage.test.tsx`
  - Modified: `Sunfish.Bridge` `Program.cs` (policy registration + endpoint mapping)
  - New types: `PropertyListDto`, `PropertySummaryDto`, `AuthenticatedTenantPolicy`, frontend `PropertyList`, `PropertySummary`
- **PR 2:**
  - New file: `accelerators/bridge/Sunfish.Bridge/Leases/LeasesEndpoints.cs`
  - New file: `apps/anchor-react/src/api/leases.ts`
  - Modified: `apps/anchor-react/src/hooks/useLeases.ts`
  - Modified: `apps/anchor-react/src/pages/LeasesPage.tsx`
  - Modified: `apps/anchor-react/src/pages/LeaseDetailPage.tsx`
  - Modified: `apps/anchor-react/src/pages/LeasesPage.test.tsx`
  - New file (if absent): `apps/anchor-react/src/pages/LeaseDetailPage.test.tsx`
  - Modified: `Sunfish.Bridge` `Program.cs` (endpoint mapping)
  - New types: `LeaseListDto`, `LeaseSummaryDto`, `LeaseDetailDto`, `LeaseTenantDto`, frontend `LeaseList`, `LeaseSummary`, `LeaseDetail`, `LeaseTenant`
- **PR 3:**
  - New file: `accelerators/bridge/Sunfish.Bridge/Maintenance/MaintenanceEndpoints.cs`
  - New file: `apps/anchor-react/src/api/maintenance.ts`
  - Modified: `apps/anchor-react/src/pages/MaintenancePage.tsx`
  - Modified: `apps/anchor-react/src/pages/MaintenancePage.test.tsx` (or new file)
  - Modified: `Sunfish.Bridge` `Program.cs`
  - Possibly new (per H6): single-line addition to `Sunfish.Blocks.Maintenance.Services.IMaintenanceService` + impl
  - New types: `CreateWorkOrderRequest`, `CreateWorkOrderResponse`, frontend mirrors
- **PR 4:**
  - Modified: `apps/anchor-react/src/api/erpnext.ts` (deprecation annotations + dev-mode console warnings)
  - New file: `apps/docs/anchor/anchor-react-rebind.md`
  - Modified: `apps/docs/anchor/toc.yml` (or equivalent)
  - New file: E2E smoke test under `apps/anchor-tauri/tests/e2e/` (or wherever the test harness lives)
  - Modified: `icm/_state/workstreams/W74-anchor-react-rebind-cohort-1.md`
  - Modified: `icm/_state/active-workstreams.md` (rendered output)

**Self-audit reminder (per ADR 0028-A10 + `feedback_council_can_miss_spot_check_negative_existence`):** COB structurally verifies each cited symbol by reading the actual file before declaring AP-21 clean. Especially:
- Confirm `AuthenticatedTenantPolicy` is genuinely absent before adding (verify in `foundation-authorization` + `Sunfish.Bridge/Authorization/`).
- Confirm `IMaintenanceService.CreateWorkOrderAsync(...)` is present (per H6) before writing PR 3's endpoint.
- Confirm `ILeaseService` has a way to fetch a single record by ID before writing PR 2's detail handler.

---

## 12. Cohort discipline + pattern catalog implications

This hand-off is the **first cohort hand-off** under the **Anchor React Bridge Rebind Roadmap** ratified by CO 2026-05-17T14-30Z. The shape it establishes — Bridge endpoint family + frontend client + hook rebind + page rebind, four PRs (three rebind + one cleanup) — is **candidate pattern-009** in the standing-patterns catalog. If PRs 1, 2, 3 ship cleanly with no post-merge incidents and no `@deviation-from-spec:` escalations, XO proposes pattern-009 for ratification at the next CO/XO standing-patterns review (per `standing-approved-patterns.md` §"Patterns proposed but not yet ratified").

Pattern-009 candidate matches:
- New top-level `/api/v1/<cluster>/*` Bridge endpoint family
- `AuthenticatedTenantPolicy` (or equivalent) — NOT cockpit-policy
- Server-side tenant scoping via `ITenantContext` — NEVER frontend query params for tenant
- Frontend client at `apps/anchor-react/src/api/<cluster>.ts`
- TanStack hook fetcher swap (same query-key shape)
- Page field-shape adjustment + empty-state copy update
- Deprecation marks (not deletion) on the corresponding `erpnext.ts` exports — wholesale deletion only at cohort cleanup

If ratified, future cohort PRs that match pattern-009 SKIP security-engineering SPOT-CHECK and arm auto-merge directly under pre-authorization.

The cluster-endpoint pattern from this hand-off is intentionally narrower than the cockpit pattern (`CockpitEndpoints.cs`). Reason: cockpit is a role-gated narrow surface (owner/spouse only); the cluster pattern is the canonical broad-authenticated-tenant surface for Anchor React. They coexist.

---

## 13. Beacon protocol

If COB hits a halt condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w74-{slug}.md` in `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/`.
- Halt the workstream + note in the W74 source file (`icm/_state/workstreams/W74-anchor-react-rebind-cohort-1.md`).
- `ScheduleWakeup 1800s`.

If COB completes a PR cleanly:

- Drop a brief `cob-status-2026-05-XXTHH-MMZ-w74-pr{N}-merged.md` beacon (or a single end-of-cohort beacon if running fast). The end-of-cohort beacon for PR 4 is the definitive flip signal.

If a `@deviation-from-spec:` arises mid-PR:

- File `cob-deviation-2026-05-XXTHH-MMZ-w74-pr{N}-{slug}.md` to inbox with the deviation, the rationale, and the proposed approach.
- Pause the PR's auto-merge arming. XO escalates to CO; CO replies via inbox.
- Resume only after CO acknowledgement.

---

## 14. Pre-authorization summary (XO recommendation to CO at hand-off review)

XO proposes Cohort 1 ship under pre-authorization with the following terms:

- **Granted scope:** all four PRs in this hand-off.
- **Conditional gates:**
  - PR 1 requires security-engineering SPOT-CHECK clean (council can override pre-auth per §Step 4).
  - PR 3 prescribes the XO-recommended non-cockpit-route variant (Halt H4); if CO at hand-off review chooses the cockpit-extension path, PR 3 is voided from pre-auth and follows full pipeline.
  - PR 4 is CO-visible regardless (ledger-flip PR).
- **PR-count cap:** 4. If a fifth PR is needed (e.g., a Cohort 1 addendum for the MaintenancePage update endpoint), it requires re-evaluation.
- **Deviation-flag enforcement:** honor-system per the ruling §Step 5 ("Honor-system on deviation flagging"); XO spot-checks each PR description before arming auto-merge.

**XO recommendation:** **grant pre-authorization** for Cohort 1. The rebind pattern is novel but bounded; the only judgment surface is PR 1 (covered by security-engineering SPOT-CHECK); subsequent PRs mirror the pattern. The savings (CO sees the cohort once at hand-off review + once at ledger-flip, instead of four times) outweigh the residual risk.

If CO declines pre-authorization, all four PRs fall back to the per-PR-CO-click model. The work doesn't change; only the merge cadence shifts.

---

## 15. Cross-references

- Spec source: `icm/02_architecture/anchor-react-bridge-rebind-roadmap.md` §2 (surface inventory) + §4 Cohort 1 table + §5 (cross-cutting concerns) + §6 (council requirements) + §9 (open questions, all three CO-resolved).
- Cluster-endpoint template: `accelerators/bridge/Sunfish.Bridge/Cockpit/CockpitEndpoints.cs`.
- Frontend-client template: `apps/anchor-react/src/cockpit/api.ts`.
- Fetcher pattern: `apps/anchor-react/src/hooks/useProperties.ts` + `useLeases.ts`.
- ADR 0088 (Path II — Anchor as all-in-one local-first runtime): `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md`.
- ADR 0031 (Bridge hybrid hosted-node-as-SaaS): `docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md`.
- Standing patterns catalog: `_shared/engineering/standing-approved-patterns.md` (pattern-005, pattern-006, pattern-007, pattern-008, candidate pattern-009).
- Pre-authorization ruling: `_shared/engineering/xo-ruling-workstream-pre-authorization.md` (Option B pilot mechanism).
- Canonical Stage 06 template: `icm/_state/handoffs/blocks-financial-ar-stage06-handoff.md`.
- Related workstreams:
  - W#60 (ERPNext composition pivot — Phase 2 React UI shipped; this hand-off completes the data-plane shift Phase 4 prepared)
  - W#62 (PropertyUnit substrate — shipped; dependency)
  - W#64 (blocks-properties EntityTag — hand-off authored; not yet built; this hand-off ships with EntityTag filter as a no-op TODO)
  - W#68 (blocks-financial-payments — gate for Cohort 2 RB-8 payments rebind)
- Memory references:
  - `feedback_anchor_smoke_test_playwright_cdp` (smoke-test pattern)
  - `feedback_never_add_workstream_rows_directly_to_ledger`
  - `feedback_worktree_base_main_not_gitbutler`
  - `feedback_council_can_miss_spot_check_negative_existence`

---

**End of hand-off.**
