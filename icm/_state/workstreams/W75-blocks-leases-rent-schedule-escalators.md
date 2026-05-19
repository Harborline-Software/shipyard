---
sort_order: 83
number: 75
slug: blocks-leases-rent-schedule-escalators
title: "W#75 — blocks-leases: rent-schedule escalators + ProjectedNextMonthRent"
status: "ready-to-build"
status_cell: "`ready-to-build` — NO gate; runs in parallel with W#73 and the W#68/W#71/W#69/W#70 cluster; hand-off at `icm/_state/handoffs/blocks-leases-rent-schedule-escalators-stage06-handoff.md`; 3 PRs; ~6-9h"
owner: "sunfish-PM (substrate PRs 1+2) + FED (UI rebind PR 3)"
owner_cell: "sunfish-PM (substrate PRs 1+2) + FED (UI rebind PR 3)"
reference_cell: "`packages/blocks-leases/Models/Lease.cs` (today: flat `MonthlyRent decimal` only) + `coordination/inbox/admiral-status-2026-05-17T23-30Z-w72-substrate-todos-triage.md` §2 + `icm/_state/handoffs/blocks-leases-rent-schedule-escalators-stage06-handoff.md`"
---

## Notes

**RentRollV2 v2-projection closure.** Adds a rent-escalator schedule as a value-object collection on `Lease`, plus `ProjectedRentForMonth(YearMonth)` as a pure function over the schedule. Closes the W#72 PR 6 `ProjectedNextMonthRent: current.MonthlyRent` v2-simplification stub at `RentRollCartridge.cs` line 346.

*Renumbered from W#74 on 2026-05-18 per `admiral-ruling-2026-05-18T23-15Z-fed-cohort1-pr4-ledger-blockers-resolved.md` — W#74 canonically owned by anchor-react-rebind-cohort-1.*

Standard review only — no mandatory council. Substrate-only; no auth, no payment, no audit-emission, no cross-cluster invariant.
