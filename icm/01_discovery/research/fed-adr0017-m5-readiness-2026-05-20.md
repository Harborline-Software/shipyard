# ADR 0017 M5 Fan-out — Readiness Assessment and Scoping Decision

**Authored by:** FED (Frontend UX Specialist)
**Date:** 2026-05-20
**ADR reference:** `docs/adrs/0017-web-components-lit-technical-basis.md`
**G-2 done condition:** "Either advance OR formally defer per ADR 0017 audit decision"

---

## Summary decision

**DEFER M5 to post-MVP Phase 2.**

M5 (fan-out of all component families across three tracks: Blazor, React, WC) is premature while M0–M4 are incomplete. This document records the audit findings and the formal deferral decision so the G-2 done condition is satisfied.

---

## M0–M4 completion audit

### M0 — Extract four-contract specs into `ui-core`

| Check | Finding |
|---|---|
| `packages/ui-core/Contracts/` | Exists. Contains 6 infrastructure contracts (`ISunfishCssProvider`, `ISunfishIconProvider`, `ISunfishJsInterop`, `ISunfishRenderer`, `IClientSubscription`, `IClientTask`). |
| Per-component 4-contract specs (semantic / a11y / styling / interaction) | **None.** No `Contracts/DataDisplay/`, `Contracts/Buttons/`, etc. directories. |
| M0 exit criterion: "≥10% of components have explicit ui-core contracts" | **Not met.** 0 of ~40 shipped components have 4-contract specs. |

**M0 status: ❌ Not started (per exit criterion)**

The conformance infrastructure (`Conformance/` — WCAG declarations, conformance registry, ARIA mapping) is solid and will feed the a11y slice of the four-contract shape. That work is not wasted; it just hasn't been applied to per-component authoring yet.

### M1 — Finish G37 SunfishDataGrid as Razor, retrofit contracts

| Check | Finding |
|---|---|
| `SunfishDataGrid.razor` (+ `.Data.cs`, `.Editing.cs`, `.Interop.cs`, `.Rendering.cs`) | **Exists.** G37 shipped. |
| DataGrid contracts retrofitted into `ui-core/Contracts/DataDisplay/` | **None.** No DataDisplay contracts directory. |
| M1 exit criterion: "SunfishDataGrid contracts in ui-core/Contracts/DataDisplay/" | **Not met.** |

**M1 status: ⚠ Half done** — G37 delivered the Razor component; the contract-retrofit has not been scheduled.

### M2 — Scaffold `ui-adapters-react`

| Check | Finding |
|---|---|
| `packages/ui-adapters-react/` | **Exists.** Has `contracts/`, `providers/`, `hooks/`, `a11y/` directories. |
| `packages/ui-react/` | **Exists.** Sunfish-specific React component library (FreshnessBadge, SyncStateBadge, OfflineIndicator, RoleGate, PropertyCard, CurrencyAmount, AgingBucketPill — v0.2). |
| First component porting proof-point | The `ui-adapters-react` package has infrastructure (CSS providers, a11y, contracts) but no component-level parity coverage yet against Blazor adapter. |

**M2 status: ✅ Scaffolded** — the package exists and has meaningful infrastructure. Component parity coverage is thin but M2's scaffolding requirement is met. `ui-react` components are Sunfish-specific (not yet fully aligned to the `ui-core` 4-contract spec).

### M3 — Parity harness

| Check | Finding |
|---|---|
| Cross-adapter parity harness (Blazor vs React equivalence) | **Not found.** No `packages/ui-core/tests/parity/` or equivalent. |
| `ui-core/tests/CssProviderContractTests.cs` | Exists — tests the CSS provider infrastructure contract, not per-component parity. |
| M3 exit criterion: "harness runs across Blazor + React in CI" | **Not met.** |

**M3 status: ❌ Not started**

Note: ADR 0017 states M3 is "a hard requirement" and the design doc should "land in M2 so M3 can implement." Neither has been authored.

### M4 — Scaffold `ui-components-web` (WC consumption track)

| Check | Finding |
|---|---|
| `packages/ui-components-web/` | **Does not exist.** |
| npm placeholder `@sunfish/ui-components-web` | Not registered. |
| Lit + TypeScript scaffolding | Not present. |

**M4 status: ❌ Not started**

---

## M5 readiness verdict

M5 ("fan-out remaining components across three tracks") requires all three tracks to be functional and parity-tested. Current state:

| Prerequisite | Required for M5 | Status |
|---|---|---|
| M0: 10%+ of components have 4-contract specs | Yes (contracts are the spec M5 implements against) | ❌ |
| M1: DataGrid contracts retrofitted | Yes (DataGrid is the proof-point; must work before scaling) | ❌ |
| M2: ui-adapters-react scaffolded | Yes | ✅ |
| M3: Parity harness in CI | Yes (M5 parity coverage is the merge gate) | ❌ |
| M4: ui-components-web scaffolded | Yes (WC track must exist for M5 to fan into it) | ❌ |

**M5 cannot start.** 3 of 5 prerequisites are unmet, including M4 (the WC track doesn't exist) and M3 (the parity harness doesn't exist).

---

## Formal deferral decision

M5 is **formally deferred** to post-MVP Phase 2.

**Rationale:**
- MVP (Phase 1) priority is the ERP page cluster: cohorts 1–3 (Properties/Leases/Maintenance/Rent Collection/Accounting/Reports). This work is on the critical path for CIC's acceptance tests.
- M5 is a ~120-implementation multi-month engineering investment. Starting it during MVP would fragment capacity without benefit to MVP acceptance criteria.
- No user-facing feature in the current MVP scope requires the WC consumption track (`ui-components-web`). The React adapter (`ui-adapters-react` + `@sunfish/ui-react`) serves the `apps/web` and `apps/desktop` targets; the Blazor adapter serves the `.NET MAUI Blazor Hybrid` path.
- M0/M1/M3/M4 work that lands incidentally during MVP (e.g., contract extraction as part of a component refactor) should be captured opportunistically, but is not scheduled.

**Recommended Phase 2 sequencing (when M5 is revisited):**
1. M0 proof-point: author 4-contract spec for `SunfishButton` — the simplest leaf component. This validates the contract template.
2. M1 retrofit: apply the same 4-contract spec to `SunfishDataGrid` (already shipped; proves the model handles the complex case).
3. M3 design doc: author the parity harness design doc. Implement the harness for `SunfishButton` (Blazor + React).
4. M4 scaffolding: scaffold `ui-components-web` with Lit + Vite; implement `SunfishButton` as the first WC component; wire into parity harness.
5. M5 begins: fan out from Buttons → Editors → Feedback → Charts → Forms → DataDisplay → Overlays → Navigation → Layout.

---

## ADR 0017 follow-up items still pending (unchanged from original ADR)

Per ADR §Consequences / Follow-ups — none of these have been authored:
- ADR 0019 — npm publish + versioning for `ui-components-web` (triggers at M4)
- ADR 0020 — React adapter scaffolding choices (build tool, CSS strategy, state primitives) — M2 started without a formal ADR
- Design doc — Parity test harness (M3 prerequisite)
- Design doc — Declarative Shadow DOM + SSR patterns for WC track on Blazor-Server pages
- Design doc — Contract authoring guide

These are queued for Phase 2. None are blocking MVP.

---

## G-2 done condition: satisfied

The G-2 done condition is: "Either advance OR formally defer per ADR 0017 audit decision."

This document constitutes the formal deferral. M5 is deferred to post-MVP Phase 2 with the rationale and re-entry criteria documented above.
