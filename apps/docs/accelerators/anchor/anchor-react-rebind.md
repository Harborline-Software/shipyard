# Anchor React → Bridge Rebind Status

Tracks the cohort-by-cohort migration of Anchor React's data plane from
`/api/v1/erpnext/*` to native Bridge cluster endpoints per ADR 0088 Path II.

## Page-by-page status

| Page | Status | Bridge endpoint | Workstream | Merged PRs |
|---|---|---|---|---|
| PropertiesPage | rebound | `/api/v1/properties` | W#74 (Cohort 1) | sunfish (initial migration) |
| LeasesPage | rebound | `/api/v1/leases` | W#74 (Cohort 1) | sunfish #7, signal-bridge #7 |
| LeaseDetailPage | rebound (payments deferred) | `/api/v1/leases/{id}` | W#74 (Cohort 1) | sunfish #7, signal-bridge #7 |
| MaintenancePage | rebound (create only; update deferred) | `/api/v1/cockpit/work-orders` | W#74 (Cohort 1) | sunfish #11, signal-bridge #11, shipyard #28 |
| AccountingPage | pending | TBD | Cohort 2 | — |
| RentCollectionPage | pending | TBD | Cohort 2 | — |
| PLReport | pending | `/api/v1/financial/reports/pl-by-property` | Cohort 3 | — |
| RentRoll | pending | `/api/v1/financial/reports/rent-roll` | Cohort 3 | — |
| (cleanup) | pending | — | Cohort 4 | — |

## ERPNext deprecation timeline

| Milestone | Routes deprecated | Routes deleted |
|---|---|---|
| Cohort 1 (2026-05-18) | `/api/v1/erpnext/properties`, `/api/v1/erpnext/leases*`, `/api/v1/erpnext/maintenance*` — marked `@deprecated` in `erpnext.ts` | None |
| Cohort 2 | + AccountingPage routes (`/api/v1/erpnext/accounting/*`, `/api/v1/erpnext/payments`) | None |
| Cohort 3 | + report routes (`/api/v1/erpnext/reports/*`) | None |
| Cohort 4 | All Cohort 1-3 marks remain | All `/api/v1/erpnext/*` routes deleted from Bridge passthrough; `erpnext.ts` deleted |

Per CO ratification 2026-05-17: ERPNext routes are kept for one milestone after deprecation
(to support migration orchestrator + give time for parallel cohort work to land), then deleted
wholesale at Cohort 4.

## Cohort 1 known temporary regressions

1. **LeaseDetailPage payment history** — `usePayments()` still calls `/api/v1/erpnext/payments`.
   Resolves in Cohort 2 RB-8 when the payments endpoint rebinds to `blocks-financial-ar` +
   `blocks-financial-payments`.
2. **MaintenancePage status updates** — the status dropdown is read-only. Cohort 1 PR 3 ships
   create only; update mutations follow in a Cohort 1 addendum or Cohort 2 work-orders writeback PR.

## Cohort acceptance gates

See `icm/02_architecture/anchor-react-bridge-rebind-roadmap.md` §4 for per-cohort acceptance
criteria.
