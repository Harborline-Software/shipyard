---
sort_order: 82
number: 74
slug: anchor-react-rebind-cohort-1
title: "W#74 — Anchor React Rebind Cohort 1 (Properties + Leases + Maintenance)"
status: "built"
status_cell: "`built` — 4 PRs merged 2026-05-18; sunfish (initial migration/PR 1), sunfish #7 + signal-bridge #7 (PR 2), sunfish #11 + signal-bridge #11 + shipyard #28 (PR 3), sunfish #12 + shipyard #32 (PR 4 close-out)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM"
reference_cell: "`icm/_state/handoffs/anchor-react-rebind-cohort-1-stage06-handoff.md`; `icm/02_architecture/anchor-react-bridge-rebind-roadmap.md` §2 + §4 Cohort 1 table; ADR 0088 §1+§3; ADR 0031"
---

## Notes

**Tauri-first pivot Cohort 1.** Rebinds PropertiesPage + LeasesPage + LeaseDetailPage + MaintenancePage from ERPNext API to native Bridge cluster endpoints. Post-rebind, those 4 pages render without ERPNext running.

```
blocks-properties   (IPropertyRepository)      ✓ on main
blocks-leases       (ILeaseService)             ✓ on main
blocks-work-orders  (cockpit/WorkOrdersEndpoint)✓ on main
  └──▶ W#74 Anchor React Rebind Cohort 1  ← THIS WORKSTREAM
        └──▶ Cohort 2 (Accounting + Payments — gated on blocks-financial-payments)
        └──▶ Cohort 3 (Reporting — gated on blocks-reports cartridges)
        └──▶ Cohort 4 (ERPNext passthrough deletion)
```

**Pre-auth status:** requested (`co-pre-authorized: requested` in hand-off frontmatter). CO approval needed before COB kickoff. PR 4 (ledger flip) always requires CO review regardless of pre-auth.

**Halt H4 (PR 3 cockpit-touch):** XO recommends non-cockpit route for Maintenance POST: `POST /api/v1/maintenance/work-orders/` instead of `POST /api/v1/cockpit/work-orders/`. This avoids the cockpit-touch "always-full-pipeline" constraint. COB halts and files `cob-question-*` if this decision was not explicitly resolved before PR 3 opens.
