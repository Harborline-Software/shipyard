---
id: 100
title: ERPNext Data Import Contract
status: Proposed
date: 2026-05-29
proposed-date: 2026-05-29
author: ONR
tier: foundation
pipeline_variant: sunfish-feature-change

concern:
  - data-migration
  - idempotency
  - partial-failure-recovery
  - multi-tenant-isolation
  - financial-data-correctness
  - clean-room-license-discipline
  - no-pii-secret-logging
  - access-mode-abstraction

enables:
  - a0-extraction-adapter
  - a1-chart-of-accounts-pass
  - a2-parties-periods-tax-pass
  - a3-opening-balances-pass
  - a4-transactional-history-pass
  - a5-reconciliation-linkage-pass
  - a6-reconcile-verify-pass
  - a7-import-cli-driver
  - cic-live-erpnext-cutover-to-sunfish

composes:
  - 84   # TenantId Sentinel Governance (an imported row MUST NOT bind to TenantId.System; the --target-tenant must be a real tenant)
  - 88   # Anchor All-In-One Local-First Runtime (the importer is ADR 0088 Path II — ERPNext is the legacy engine being retired; clean-room §3 discipline)
  - 91   # ITenantContext Divergence Resolution (every importer write is IMustHaveTenant via the Authorization sum-interface facade, NOT the MultiTenancy narrowed variant — signal-bridge#34 trap)
  - 93   # Stage-05 Adversarial Review Protocol (the dual-council MANDATORY halt-condition set on this substrate-tier contract)
  - 97   # PasswordHasher Substrate (substrate-tier ADR cadence + production-guard / fail-closed IHostedService precedent + Halt-9 dual-council pattern)
  - 98   # Block-Naming Generalization (the spec's stale package names reconciled to the shipped block names; substrate-tier dual-council MANDATORY precedent)
  - 99   # First-Party Session Establishment (the positive-testable-non-bypassable enforcement-invariant discipline; cited-symbol audit discipline)

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

requires-council:
  - dotnet-architect
  - security-engineering

co-pre-authorized: false  # substrate-tier contract; writes CIC's REAL financial books across 7 packages — ADR text + the A0 extraction-adapter PR carry mandatory dual-council per H8 / §"Council review"
---

# ADR 0100 — ERPNext Data Import Contract

**Status:** Proposed (Rev 1; awaiting dual-council MANDATORY attestation — security-engineering + .NET-architect). Substrate-tier: the importer writes CIC's REAL financial books (chart, journal entries, invoices, bills, payments, parties) across 7 `blocks-financial-*` / `blocks-people-foundation` packages into a live Sunfish tenant. Dual-council MANDATORY on this ADR text AND on the A0 extraction-adapter PR per §"Council review".

**Date:** 2026-05-29

**Resolves:** The cross-cutting *contract* that all the ERPNext-importer build units (Workstream A: A0–A7 per the post-MVP WBS) depend on. Three per-record idempotent upserters were already shipped piecemeal (`IErpnextAccountImporter`, `IErpnextJournalEntryImporter`, `IErpnextSalesInvoiceImporter`) — and four MORE have landed since the WBS was written (purchase-invoice/AP, party, fiscal-year/period, tax) — **with no ratified contract governing them.** That ungoverned drift has already produced three live divergences (§"Drift the contract resolves"): the tenant-scoping parameter is positioned differently on each interface; the external-reference idempotency key is stored three different ways (a dedicated `ExternalRef` field on `GLAccount`, `Party.Tags`, `Bill.Notes`); and the shipped `ImportAction` enum has no reject/failure variant, so the spec's reject-bin + partial-failure semantics have nowhere to land. This ADR is the ratification that freezes the contract before A1+ orchestration builds on top of seven inconsistent upserters and CIC's real $7.6M-history books go through them.

**Decision (settled at dispatch):** BUILD the one-way ERPNext → Sunfish-native importer now. CIC is the customer; importing CIC's live local ERPNext data into a Sunfish tenant is the G-1 done-condition "the PM business runs on the app" (post-MVP WBS, Workstream A `[ACTIVE]`, per CIC ruling 2026-05-29). This ADR does NOT decide *whether* to build (settled) — it pins the contract the build inherits and resolves the 10 open questions the Stage-03 spec left for ratification.

**Predecessor design + WBS:**
- `shipyard/_shared/engineering/erpnext-to-anchor-migration-importer-spec.md` (1241 lines; XO 2026-05-16) — the Stage-03 six-pass design + the 10 open questions (§10) this ADR folds. **Its package-name table is STALE** — reconciled to the shipped block names in §"Stale-spec reconciliation".
- `shipyard/icm/05_implementation-plan/post-mvp-wbs-2026-05-29.md` (ONR; shipyard#181 Workstream A) — the A0–A8 + A-ADR decomposition this ADR is the "A-ADR" of. **Its "3 shipped slices" table is itself now stale** — 7 upserters are on `main` (§A0 cited-symbol audit).

---

## A0 cited-symbol audit

Per the ADR 0093 / 0096 / 0097 / 0099 cited-symbol audit discipline. Classifications: **Existing & verified** (on `shipyard/main` at authoring, path-checked); **Introduced by this contract** (ships in an A-unit PR); **In-flight** (file exists on an OPEN/local branch, not yet on main).

| Symbol / Path | Classification | Verified |
|---|---|---|
| `Sunfish.Blocks.FinancialLedger.Migration.ImportOutcome<T>` (`(T Record, ImportAction Action, string? Detail)`) | **Existing** — the shipped per-record return shape. Has NO reject/error variant. | yes — `packages/blocks-financial-ledger/Migration/ImportOutcome.cs` (record; 3 fields). NOTE: each cluster ships its OWN copy (`blocks-financial-ar/Migration/ImportOutcome.cs`, `blocks-financial-ap/...`, `blocks-people-foundation/...`) — a duplication the contract addresses (D7). |
| `Sunfish.Blocks.FinancialLedger.Migration.ImportAction` (enum `Inserted \| Updated \| Skipped`) | **Existing** — the shipped per-record outcome marker. **No `Rejected` member.** | yes — `packages/blocks-financial-ledger/Migration/ImportAction.cs` |
| `IErpnextAccountImporter.UpsertFromErpnextAsync(ErpnextAccountSource, ChartOfAccountsId targetChart, ct)` | **Existing** — Pass-1 chart upserter. **Takes NO `TenantId`** (tenant implied by chart). | yes — `packages/blocks-financial-ledger/Migration/IErpnextAccountImporter.cs` |
| `IErpnextJournalEntryImporter.UpsertFromErpnextAsync(TenantId, ErpnextJournalEntrySource, ChartOfAccountsId, ct)` | **Existing** — Pass-3/4.4 JE upserter. **`TenantId` is the FIRST positional param.** Posted entries immutable → re-import returns `Skipped` (warning detail on field drift). | yes — `packages/blocks-financial-ledger/Migration/IErpnextJournalEntryImporter.cs` |
| `IErpnextSalesInvoiceImporter.UpsertSalesInvoiceAsync(...)` | **Existing** — Pass-4.1 sales-invoice → `Invoice` upserter. | yes — `packages/blocks-financial-ar/Migration/IErpnextSalesInvoiceImporter.cs` |
| `IErpnextPurchaseInvoiceImporter.UpsertPurchaseInvoiceAsync(ErpnextPurchaseInvoiceSource, TenantId, ChartOfAccountsId, PartyId vendorPartyId, GLAccountId apAccountId, GLAccountId defaultExpenseAccountId, ct)` | **Existing** (NOT in the WBS's "3 shipped" table) — Pass-4.2 AP/bill upserter. **`TenantId` is SECOND.** Stores `ExternalRef="erpnext:pinv:{Name}"` + `erpnextModified:{Modified}` in `Bill.Notes`. No GL re-post in v1. | yes — `packages/blocks-financial-ap/Migration/IErpnextPurchaseInvoiceImporter.cs` |
| `IErpnextPartyImporter.UpsertCustomerAsync(ErpnextCustomerSource, TenantId, PartyId actor, ct)` + `UpsertSupplierAsync(...)` | **Existing** (NOT in the WBS table) — Pass-2 party upserter. **`TenantId` is SECOND.** Idempotent on `(Name, Modified)`; stores `externalRef:erpnext:customer:{Name}` + `erpnextModified:{Modified}` in **`Party.Tags`** "so we don't lock the schema shape until the migration-importer convention stabilizes across all clusters." | yes — `packages/blocks-people-foundation/Migration/IErpnextPartyImporter.cs` |
| `IErpnextFiscalYearImporter` + `IErpnextFiscalPeriodImporter` | **Existing** (NOT in the WBS table) — Pass-2 period upserters. | yes — `packages/blocks-financial-periods/Migration/` |
| `ErpnextTaxImporter` | **Existing** (NOT in the WBS table) — Pass-2 tax upserter. | yes — `packages/blocks-financial-tax/Migration/ErpnextTaxImporter.cs` |
| `IErpnextProjectImporter` (external-ref via `Tags`, per its xmldoc the convention the Party importer copied) | **Existing** — non-financial; establishes the `Tags`-based external-ref precedent. | yes — `packages/blocks-work-projects/Migration/IErpnextProjectImporter.cs` |
| `ErpnextAccountSource` (`Name, Modified, AccountName, AccountNumber?, ParentAccountName?, AccountType?, IsGroup, Disabled`) | **Existing** — the parsed in-memory source record the upserter consumes. xmldoc says "as exported via REST" — but the record is **access-mode-agnostic** (just a DTO); the REST mention is illustrative, not binding (D6). | yes — `packages/blocks-financial-ledger/Migration/ErpnextAccountSource.cs` |
| `ErpnextJournalEntrySource` (+ `ErpnextJournalEntryLineSource`) | **Existing** — header + lines DTO; `DebitInAccountCurrency`/`CreditInAccountCurrency` are `decimal`; `IsOpening` drives the Pass-3 filter; `DocStatus` int. | yes — `packages/blocks-financial-ledger/Migration/ErpnextJournalEntrySource.cs` |
| `GLAccount.ExternalRef` (dedicated string field) | **Existing** — the chart upserter's idempotency key storage. The ONLY block-entity with a first-class `ExternalRef` field; Party/Bill use `Tags`/`Notes` instead. | yes — referenced by `IErpnextAccountImporter` xmldoc (`GLAccount.ExternalRef == source.Name`) |
| `IMustHaveTenant` + `Sunfish.Foundation.Authorization.ITenantContext` (sum-interface facade) | **Existing** — the tenant-scoping contract every importer write MUST satisfy. The facade (NOT `MultiTenancy.ITenantContext`) is the binding surface (ADR 0091 R2; signal-bridge#34 CS0104 trap). | yes — `packages/foundation-authorization/` (ADR 0091 Step 1, on main) |
| `TenantId.IsSystemSentinel` | **Existing** — rejects `default(TenantId)`, `__system__`, any `__`-prefixed sentinel. The `--target-tenant` validation (H3 / S3) reuses THIS guard. | yes — `packages/foundation-multitenancy/TenantQueryFilterExtensions.cs:159` (ADR 0084; sec-eng C1 2026-05-21) |
| `fix-erpnext-importer-tenant-positional` (local branch; not on origin) | **In-flight** — an in-progress fix already targeting divergence D1. This ADR ratifies the canonical signature it should converge to (D1) so the fix lands against a frozen contract, not an ad-hoc one. | yes — `git branch` (local; `git log origin/...` empty) |
| ADR 0088 Path II + §3 clean-room discipline | **Existing** | yes — `docs/adrs/0088-anchor-all-in-one-local-first-runtime.md` |
| ADR 0091 R2 (facade + tenant cross-check) / ADR 0084 (sentinel) / ADR 0093 (Stage-05) / ADR 0097–0099 (substrate cadence) | **Existing** | yes — `docs/adrs/` (all on main) |

§A0 totals (Rev 1): 18 cited references. Existing & verified: 16. Introduced by this contract: 1 (`ImportFailure` reject channel — D2). In-flight: 1 (`fix-erpnext-importer-tenant-positional` branch).

> **The headline §A0 finding:** the WBS said 3 upserters shipped; **7 are on main** (account, JE, sales-invoice, purchase-invoice/AP, party, period, tax — plus the non-financial project importer). They shipped piecemeal with **no governing contract**, and they have already drifted apart on three contract dimensions (§"Drift the contract resolves"). This is the empirical case for the ADR: not a speculative future need, but live, on-disk inconsistency in code that will write CIC's real books.

---

## Context

ADR 0088 Path II makes Anchor/Sunfish the all-in-one local-first runtime and ERPNext the legacy engine being retired. The cutover requires a one-way importer that reads CIC's live ERPNext data and writes Sunfish-native domain records. The Stage-03 spec (`erpnext-to-anchor-migration-importer-spec.md`) designed a six-pass importer (chart → reference data → opening balances → transactional history → reconciliation linkage → verify) with an idempotency contract, per-pass commit boundaries, a reject-bin, and a Pass-6 reconciliation gate. That spec is a **design doc, not a ratified contract** — it explicitly defers 10 open questions (§10) "for CO/cob ratification before Stage 06 build."

Meanwhile, build proceeded ahead of ratification: seven per-record upserters shipped onto `main` over the cohort waves, each authored in isolation. The result is exactly the divergence a contract exists to prevent — and because the next build units (A1–A7) are *orchestration over these upserters*, the orchestrator would otherwise have to accommodate three inconsistent calling conventions, an idempotency-key storage strategy that differs per entity, and a per-record outcome type that cannot express failure. CIC's real financial history ($7.6M of Wave-migrated accounting across 4 LLCs per the spec §1.1) would be written through that inconsistency.

This is the moment to freeze the contract: the upserters exist (so the contract is grounded in shipped reality, not speculation), but the orchestration does not (so freezing now costs a small convergence refactor, not a rewrite of a working importer).

---

## Drift the contract resolves (the empirical case)

Three live divergences on `main`, each a contract decision this ADR settles:

**D1 — Tenant-scoping parameter is positioned three different ways.**
- `IErpnextAccountImporter.UpsertFromErpnextAsync(source, ChartOfAccountsId targetChart, ct)` — **no `TenantId`** (tenant implied by the chart).
- `IErpnextJournalEntryImporter.UpsertFromErpnextAsync(TenantId, source, ChartOfAccountsId, ct)` — `TenantId` **first**.
- `IErpnextPartyImporter.UpsertCustomerAsync(source, TenantId, PartyId actor, ct)` and `IErpnextPurchaseInvoiceImporter.UpsertPurchaseInvoiceAsync(source, TenantId, ...)` — `TenantId` **second**.

A local `fix-erpnext-importer-tenant-positional` branch is already trying to fix this — but without a ratified target it will just pick one convention by author preference. The contract names the canonical signature (D1 decision) so the fix converges deterministically and the orchestrator (A7) holds the tenant once and threads it identically into every pass.

**D2 — The idempotency external-reference key is stored three ways.**
- `GLAccount` — a dedicated first-class `ExternalRef` string field.
- `Party` — two `Tags` entries (`externalRef:erpnext:customer:{Name}` + `erpnextModified:{Modified}`), with an explicit xmldoc rationale: "so we don't lock the schema shape until the migration-importer convention stabilizes across all clusters."
- `Bill` — `ExternalRef="erpnext:pinv:{Name}"` plus `erpnextModified:{Modified}` smuggled into `Bill.Notes`.

The Party importer's xmldoc literally asks for this ADR ("until the migration-importer convention stabilizes"). The contract stabilizes it (D2 decision).

**D3 — `ImportAction` has no failure variant, so partial-failure has nowhere to land.**
The shipped `ImportAction` enum is `Inserted | Updated | Skipped`. The spec's reject-bin (§6.3) and partial-failure semantics (§6.2/§6.4) require a per-record "this record could not be imported, here's why" outcome. The shipped `ImportOutcome<T>` is `(T Record, ImportAction, string? Detail)` — it cannot represent a record that produced NO local record. The contract adds the reject channel (D2-failure decision) without breaking the three happy-path outcomes the orchestrators already consume.

---

## Stale-spec reconciliation (build against THESE names)

The Stage-03 spec predates the ADR 0098 block-naming wave and the cluster consolidations. Build the importer against the shipped names, not the spec's:

| Spec says (stale) | Shipped reality (build against) |
|---|---|
| `blocks-financial-chart` | `blocks-financial-ledger` (chart folded in; `GLAccount` + `ChartOfAccountsId` live here) |
| `blocks-people-*` (plural cluster) | `blocks-people-foundation` (singular; `Party` / `PartyRole` / `Customer` / `Vendor`) |
| `blocks-financial-budget` | NOT shipped — budget import stays deferred (out of A-scope) |
| `blocks-property-*` | `blocks-properties` + `blocks-leases` (CONDITIONAL — only if CIC's instance has custom DocTypes; H7) |
| `externalRef: {source, id, version}` 3-field shape (spec §5.1) | shipped reality is a SINGLE natural key `(Name, Modified)`; `source` is implicitly `"erpnext"`. The contract ratifies the simpler shipped shape (D2), NOT the spec's 3-field shape. |
| `anchor import erpnext` CLI verb | keep the verb; the host binary is the Sunfish/Anchor runtime CLI (A7) |

---

## Decision

Ratify the eight contract clauses below (**C1–C8**). Each names a positive, testable, non-bypassable enforcement invariant — not just an obligation on author memory — per the ADR 0099 sec-eng lesson (an obligation the author "must remember" is a CSRF-exposed `/auth/login` waiting to happen; the contract must make violation impossible, or detectable at startup / by an architecture test).

### C1 — Idempotency / re-runnability (RATIFIES spec §5; resolves OQ-8)

**Decision.** Every per-record upsert is idempotent on the natural key **`(externalRef, Modified)`** where `externalRef` is the ERPNext `name` (stable, DocType-scoped-unique) and `Modified` is the ERPNext `modified` timestamp string (opaque, lexicographically-ordered version key). Re-run semantics:
- absent → `Inserted`;
- present and `Modified` equal → `Skipped` (no SQL UPDATE issued);
- present and source `Modified` **strictly greater** → `Updated`;
- present and source `Modified` **strictly less** → `Skipped` + warning detail (clock-drift / stale re-export guard);
- present and the local record is **immutable** (a posted `JournalEntry`) → `Skipped` + warning detail naming the drifted field — **never** a silent overwrite of posted financial state.

**Storage of `externalRef`** is canonicalized by C7 (D2). **Deletion is never inferred** from a missing source record (spec §5.3); the key-set is monotonic across re-runs.

**Enforcement invariant (testable).** Acceptance test C-IDEM: run the full importer twice against the same source → the second run produces **zero** `Inserted`/`Updated` outcomes (all `Skipped`); flip exactly one source record's `Modified` forward → exactly one `Updated`; add one source record → exactly one `Inserted` (spec §7.3 C1/C2/C3). A partial import that halted at pass N is resumable via `--from-pass N` (C2) and produces no double-post because passes 1..N-1 re-run as all-`Skipped`.

### C2 — Partial-failure semantics (RATIFIES spec §6.2/§6.4; resolves OQ via the reject channel)

**Decision — commit boundaries per pass** (the spec §6.2 table, ratified):

| Pass | Commit boundary | Failure semantics |
|---|---|---|
| 1 — Chart | single transaction over all accounts | roll back; no accounts persisted (the tree is all-or-nothing) |
| 2 — Reference data | one transaction per data family (periods / tax / parties / cost-centers) | a sub-pass failure leaves earlier sub-passes intact |
| 3 — Opening balances | per-JE transaction | per-record reject; pass completes if any opening JE succeeds |
| 4 — Transactional | per-record transaction per sub-pass | per-record reject; sub-pass completes |
| 5 — Reconciliation linkage | single transaction | roll back; no links persisted |
| 6 — Verify | **no writes** (read-only) | a failed invariant HALTS + surfaces the report |

**Decision — the reject channel (resolves D3).** Add a NON-BREAKING failure representation: a separate `ImportFailure(string ExternalRef, string DocType, string ReasonCode, string? Detail)` record carried out-of-band from `ImportOutcome<T>` (the orchestrator collects failures; the upserter either returns the happy-path `ImportOutcome<T>` or throws a typed `ImportRejectException` the orchestrator catches into an `ImportFailure`). **`ImportAction` is NOT extended with a `Rejected` member** — adding an enum member is a breaking change for the seven shipped upserters' exhaustive switches; the reject channel sits beside the outcome type instead. (.NET-architect to confirm the throw-vs-return-discriminated-union shape at attestation — see OQ-A.)

**Decision — resume point.** `--from-pass N` (1..6) resumes a halted run; the C1 idempotency contract guarantees passes 1..N-1 re-run as all-`Skipped`. No torn/orphaned financial state: posted JEs are immutable (C1), and a pass that rolls back (1/5) leaves nothing; a per-record pass (3/4) leaves only successfully-committed records, each idempotently skipped on resume.

**Enforcement invariant (testable).** Acceptance test C-REJECT: a source with one deliberately-broken record (e.g., a JE referencing an unknown account) produces exactly one `ImportFailure` with a structured `ReasonCode`, the pass completes (for per-record passes) or halts cleanly with zero partial writes (for transactional passes 1/5), and a resume run re-imports only the previously-failed/absent records.

### C3 — Tenant-scoping invariant (RATIFIES the directive's hardest requirement; resolves D1)

**Decision — canonical signature.** Every importer write is tenant-scoped via `IMustHaveTenant`, bound through the **`Sunfish.Foundation.Authorization.ITenantContext` sum-interface facade** — **NOT** `Sunfish.Foundation.MultiTenancy.ITenantContext` (the narrowed variant; mixing them is the signal-bridge#34 CS0104 build-break + the consumption-qualification trap). The canonical upserter signature places **`TenantId` first** (the convention `IErpnextJournalEntryImporter` already uses): `Upsert…Async(TenantId tenant, <ErpnextXSource> source, <cross-cluster ids…>, CancellationToken ct)`. The account upserter (D1, no `TenantId` today) gains the leading `TenantId`; the party/AP upserters (D1, `TenantId` second) move it first. The `fix-erpnext-importer-tenant-positional` branch converges to this.

**Decision — one tenant per run.** The importer targets **exactly one** Sunfish tenant per invocation (`--target-tenant <id>`). The CLI validates the target with `TenantId.IsSystemSentinel` and **fails closed** if the target is `default`, `__system__`, or any `__`-prefixed sentinel (reuses the ADR 0084 guard, not a fresh check). No cross-tenant bleed: the orchestrator holds the single resolved `TenantId` and threads the identical value into every upsert call; no pass may derive a tenant from source data.

**Enforcement invariant (testable + startup).** Acceptance test C-TENANT: (a) every imported row's `TenantId` equals the `--target-tenant`; (b) an architecture test asserts every `Upsert…Async` signature in `*/Migration/` takes `TenantId` as the first parameter (mechanical, prevents D1 regression); (c) a `--target-tenant` of any sentinel value fails the CLI pre-flight with a clear error before any pass runs. The DbContext's existing tenant-query-filter (ADR 0091/0092) provides the data-plane backstop.

### C4 — Read-only clean-room source posture (RATIFIES spec §1.2 / §9; resolves OQ-6)

**Decision.** The importer is **strictly read-only against the ERPNext source** and never writes back (spec §1.2). Extraction reads ERPNext's **data format only** — a public data-interchange contract (DocType field labels as users see them + public API docs) — NOT Frappe/ERPNext controllers, validators, workflow code, or DocType-definition JSON (spec §9.1/§9.2). The export-production step (whichever access mode — C6) is a **CIC one-time task with documented commands**; the importer does NOT SSH into or drive a live Frappe runtime (spec §10.6 (a), recommended; (b) rejected). Source-header attribution names ERPNext + Frappe + GPLv3 + "format-reference-only; no code derived" (spec §9.5).

**Enforcement invariant (testable).** Acceptance test C-CLEANROOM: (a) the extraction adapter exposes only read operations (no write/update/delete method against the source); (b) a license/attribution header is present on the extraction adapter; (c) for the MariaDB-dump mode (C6), the adapter opens the dump read-only and issues only `SELECT` (no DDL/DML against the source DB). sec-eng owns this review at the A0 PR.

### C5 — Mapping authority — explicit, versioned, fail-loud (resolves OQ-1)

**Decision.** The ERPNext-DocType → Sunfish-block-entity mapping is **explicit and versioned** (the spec §3 tables are the canonical v1 mapping; the importer pins them in code, derived from public docs / observed export shape per C4). **Unmapped or unknown DocTypes / enum values FAIL LOUD — never silently dropped:**
- an unknown `account_type` after the parent-walk → HALT Pass 1 (`UnknownAccountType`), not a guess;
- an unknown `voucher_type` → import as `Manual` **with a warning surfaced in the Pass-6 report** (a known, bounded fallback — not silent);
- a DocType file the importer doesn't understand → logged + counted in the report's `_unmapped/` section (spec §2.2), not an error, but VISIBLE;
- a custom `Property`/`Lease` DocType present-or-absent is a CIC pre-flight input (H7), not a runtime guess.

**Enforcement invariant (testable).** Acceptance test C-MAP: a source with an unmappable `account_type` halts Pass 1 with a structured reject (spec §7.4 D1-class) and zero accounts persisted; an unknown `voucher_type` imports as `Manual` and appears in the report's warnings section; a DocType outside the v1 mapping appears in the report's unmapped section with a non-zero count. No code path drops a financial record without a corresponding report line.

### C6 — Access-mode abstraction behind the A0 seam (resolves OQ-6; recommend dump)

**Decision.** The contract is **access-mode-agnostic**. The shipped `Erpnext*Source` records are plain DTOs (the "exported via REST" xmldoc is illustrative, not binding — D6 reconciliation); the A0 extraction adapter is the seam that produces those DTOs from ONE of three modes: (a) ERPNext REST API; (b) **MariaDB dump ⭐ RECOMMENDED**; (c) per-DocType CSV/JSON export. **ONR recommends (b) the MariaDB dump** — it matches the spec's static-input read-only clean-room design (§1.2/§9), is offline + deterministic + re-runnable (best fit for the C1 idempotency contract + dry-run), avoids REST pagination/rate-limit complexity, and is the cleanest long-term option (per `feedback_prefer_cleanest_long_term_option`). (a) is the fallback if CIC's instance is remote-only; (c) is the spec's documented path if CIC already has an export. The final mode is a CIC input (H-CIC-1) — A0's design half recommends, A0's build half implements the chosen mode behind the seam.

**Enforcement invariant (testable).** Acceptance test C-MODE: the upserters + orchestrator depend ONLY on the `Erpnext*Source` DTOs, never on a mode-specific type; swapping the extraction adapter (dump ↔ CSV) requires zero changes to A1–A6. The DTOs carry no access-mode-specific field.

### C7 — Canonical external-ref storage (resolves D2)

**Decision.** Standardize on the **`Tags`/structured-projection** convention the Party + Project importers already use, NOT a per-entity bespoke field, for entities that do not already have a first-class `ExternalRef`. Concretely: external-ref is stored as a structured `externalRef:erpnext:<doctype>:<name>` token + an `erpnextModified:<modified>` token, on whichever the entity's canonical extensible-metadata surface is (`Tags` for Party/Project; the existing `ExternalRef` field for `GLAccount` is GRANDFATHERED and stays — migrating it is out of A-scope and the C1 lookup tolerates both via a small resolver). **`Bill.Notes` is NOT a sanctioned external-ref home** — `Notes` is user-facing free text; the AP importer is amended (in its A4.2 orchestration PR) to move its external-ref to the same `Tags` convention (or a `Bill.ExternalRef` field if .NET-arch prefers a first-class field fleet-wide — OQ-B). The contract names ONE convention; the small convergence is folded into the A-unit PRs that already touch each importer.

**Enforcement invariant (testable).** Acceptance test C-EXTREF: every imported entity is locatable by `(doctype, name)` via a single shared `ExternalRefResolver`; no importer stores its idempotency key in a user-facing free-text field (`Notes`/`Memo`/`Description`); an architecture test asserts the resolver covers all seven (+future) importers.

### C8 — Reconcile / verify contract — the correctness gate (RATIFIES spec §4.6 / §7.2; resolves OQ-5)

**Decision.** Pass 6 is the verifiable PASS/FAIL gate that proves the import preserved CIC's books, **read-only** (no writes; the report is the only output):
- **Trial balance** per chart: `|Σ debit − Σ credit| == 0` — **hard zero** (integer-minor-units arithmetic makes this exact when every line balances; a non-zero is a defect halt, not a tolerance). (spec §7.2 B1)
- **Per-account balance diff** vs a CIC-produced `gl-balances-snapshot.json` — threshold **$0.01** per account. (B-class)
- **AR / AP aging diff** vs CIC-produced `ar-aging-snapshot.json` / `ap-aging-snapshot.json` — threshold **$0.01** per customer/vendor per bucket. (B2/B3/B6)
- **Per-DocType count reconciliation** — every `docstatus==1` source record of a mapped DocType has exactly one corresponding local record (spec §7.1 A1-A4).
- **Wave-history total** — Σ opening balances across all LLCs within **$1.00** (looser, acknowledging the prior Wave→ERPNext migration's own rounding; resolves OQ-5: keep $1.00, CIC can tighten via the snapshot). (B5)
- **Output** — `migration-report.md` with the seven sections (run summary, trial-balance, AR/AP aging, per-account diff, reject-bin, unapplied payments, cost-center resolution; spec §4.6 step 6).

If the CIC reconcile snapshots are absent (H-CIC-4 optional), Pass 6 still runs the trial-balance hard-zero invariant + per-DocType count reconciliation, and reports aging/balance as "no snapshot — not cross-checked." A trial-balance failure is a **hard halt**; an aging diff over threshold halts unless `--allow-aging-drift` (the diff is still recorded).

**Enforcement invariant (testable).** Acceptance test C-VERIFY: post-import trial balance is exactly zero for each chart; with snapshots present, per-account + aging diffs are within threshold or the run halted; `migration-report.md` exists with all seven sections; the per-DocType count section shows zero unaccounted `docstatus==1` records. This is the gate that lets CIC declare the import done.

### C9 — No-PII / no-secret logging (cross-cutting; sec-eng)

**Decision.** Import logs (stderr, the run-scoped `import.log`, and the `migration_audit_log` table) **never emit credential, PII, or financial-record CONTENTS**. They emit: DocType, `externalRef` (the ERPNext `name` — an opaque id, not PII), outcome (`Inserted`/`Updated`/`Skipped`/`Rejected`), reject `ReasonCode`, pass/run id, counts, and diff *magnitudes* in the report. They do NOT emit: ERPNext API keys / DB-dump credentials (C4/C6), party names / emails / phones (`Party` PII), or per-line monetary amounts in the log stream (amounts appear only as aggregate diffs in the report). The MariaDB-dump credential (C6 mode b) is consumed from a CLI flag / env var and never echoed.

**Enforcement invariant (testable).** Acceptance test C-LOG: a log-capture test asserts no credential pattern, no `Party.Email`/`Phone`/`Name` value, and no per-record monetary amount appears in the captured log/audit output for a fixture import; the audit row schema (spec §6.3) carries only `source_id` + `outcome` + structured `reject_reason` columns, no content blob beyond a bounded structured `reject_detail`. sec-eng owns this at the A0 + A2.1 (party PII) PRs.

---

## Open questions for council / CIC (folded the spec's §10; flagged the residue)

The spec's 10 open questions (§10) are RESOLVED into the clauses above where ONR has authority, and surfaced to council/CIC where they need a ruling:

| Spec OQ | Disposition |
|---|---|
| §10.1 custom `Property`/`Lease` DocTypes present? | **CIC input** (H-CIC-2 / H7). A5b conditional on the answer. |
| §10.2 multi-currency v1 posture | **RESOLVED** — reject + log non-base-currency records (spec §3.5); defer multi-currency to v2. CIC's 4 LLCs are USD (guardrail, not common path). |
| §10.3 payment-application heuristic ±7 days | **RESOLVED** — ship ±7 days default + `--date-tolerance N` flag. |
| §10.4 cost-center ambiguity strict flag | **RESOLVED** — add `--cost-center-strict` (default off; falls through to `Classification`). |
| §10.5 Wave-history $1.00 tolerance | **RESOLVED in C8** — keep $1.00; CIC tightens via snapshot if desired. |
| §10.6 export-script ownership | **RESOLVED in C4** — CIC one-time documented task; importer never drives live Frappe. |
| §10.7 `rent-collection.Invoice` wrapper | **RESOLVED** — importer writes canonical financial-AR `Invoice` directly; does NOT create wrapper records (spec §12). |
| §10.8 opening-balance source-kind | **RESOLVED** — tag every `is_opening` JE `sourceKind=Migration`; `entryDate` disambiguates waves. |
| §10.9 reject-bin remediation flow | **DEFERRED** — `review-rejects` sub-command is Phase-1.5 polish, out of A-scope (flag, not build). |
| §10.10 performance budget | **RESOLVED** — <5 min for CIC's ~10K-record portfolio; profile during build, surface any pass >30s. |

**Residual questions requiring a council/CIC ruling (this Rev expects to surface, not pre-decide):**

- **OQ-A (.NET-arch).** Reject channel shape (C2/D3): typed `ImportRejectException` caught by the orchestrator into an `ImportFailure` record (ONR's lean recommendation, zero change to the seven shipped happy-path switches) **vs** a discriminated-union `ImportOutcome` redesign (cleaner long-term but touches all seven upserters + their consumers now). ONR recommends the exception channel for A-scope; flag for .NET-arch.
- **OQ-B (.NET-arch).** External-ref storage convergence (C7/D2): standardize on `Tags` tokens fleet-wide **vs** add a first-class `ExternalRef` field to every importable block-entity (matching `GLAccount`). ONR recommends `Tags` (matches the 2 importers that explicitly chose it + avoids 5 schema migrations); flag for .NET-arch.
- **H-CIC-1 (CIC).** Access mode (C6): confirm MariaDB dump (ONR recommendation) vs REST vs CSV.
- **H-CIC-2 (CIC).** DocType inventory: custom `Property`/`Lease` present? which LLCs are charts? multi-currency present?
- **H-CIC-3 (CIC).** Target Sunfish tenant id for the import.
- **H-CIC-4 (CIC, optional).** Reconcile snapshots (gl-balances / ar-aging / ap-aging) available?

---

## Stage-05 adversarial review — halt conditions (dual-council MANDATORY)

Per ADR 0093 (Stage-05 Adversarial Review Protocol). This contract is substrate-tier and writes CIC's real financial books → **dual-council MANDATORY (security-engineering + .NET-architect)** on this ADR text AND on the A0 extraction-adapter PR (the credential/clean-room surface). The following are the ADR-text halt conditions the councils MUST clear:

- **H1 (sec-eng + .NET-arch).** C3 tenant-scope: confirm the canonical `TenantId`-first signature + the `Authorization` facade binding (not `MultiTenancy`) + the `IsSystemSentinel` fail-closed `--target-tenant` validation + the architecture test that prevents D1 regression. A tenant derived from source data anywhere = RED.
- **H2 (.NET-arch).** C1 idempotency: confirm posted-`JournalEntry` immutability is enforced (re-import returns `Skipped`, never overwrites) and the `(externalRef, Modified)` lexicographic version-compare is sound (ERPNext emits ISO-8601 → lexicographic == temporal; confirm no locale/format edge that breaks the ordering).
- **H3 (.NET-arch).** C2 partial-failure: confirm the per-pass commit-boundary table is correct (transactional 1/5 vs per-record 3/4) and the reject-channel shape (OQ-A) leaves no torn financial state on a mid-pass halt + resume.
- **H4 (sec-eng).** C4 clean-room: confirm read-only-against-source (no write-back, no live-Frappe coupling) + the data-format-only license posture + attribution header. A write path to the source = RED.
- **H5 (sec-eng).** C9 no-PII/secret-logging: confirm the log/audit surface emits no credentials, no `Party` PII, no per-record monetary contents; confirm the C6-mode-b dump credential is never echoed.
- **H6 (.NET-arch).** C8 verify: confirm the trial-balance hard-zero invariant is exact under integer-minor-units and the Pass-6 gate cannot pass on a torn import (defense-in-depth: if Pass 4 rejected an imbalanced JE, the trial balance still ties).
- **H7 (CIC pre-flight, surfaced).** Custom `Property`/`Lease` DocType disposition (A5b conditional) — not a code halt; a build-scoping input.
- **H8 (Admiral).** Council cadence RATIFY: dual-council MANDATORY on ADR text + A0 PR; **test-eng MANDATORY** on the data-correctness passes (A1 toposort, A4.2/A4.3 financial allocation, A6 reconcile) per the WBS council column; sec-eng MANDATORY on A0 (credential/clean-room) + A2.1 (party PII).

If a council returns AMBER, Admiral folds amendments into Revision 2 (the ADR 0095/0096/0097/0098/0099 R2 cadence) and re-attests. ONR's prior (per the substrate-tier track record): expect Rev 1 AMBER on the OQ-A/OQ-B shape decisions + at least one C-clause enforcement-invariant tightening → fold → re-attest → Accept.

---

## Consequences

**Positive:** freezes the contract before A1–A7 orchestration builds on seven inconsistent upserters; resolves three live on-disk divergences (D1/D2/D3) that would otherwise compound; folds the spec's 10 open questions into ratified decisions; gives every A-unit a clear council/test-eng requirement; makes CIC's real-books import a verifiable PASS/FAIL gate (C8) rather than a hope; preserves the read-only clean-room license posture (C4) the cutover depends on.

**Negative / cost:** a small convergence refactor on the seven shipped upserters (D1 signature, D2/C7 external-ref home, D3 reject channel) — but each is folded into an A-unit PR that already touches that importer, not a separate rework wave. The reject channel (OQ-A) and external-ref convergence (OQ-B) need a .NET-arch ruling before A2/A4 build (gated, not blocking — A0 + A1 proceed).

**Reversibility:** the `Erpnext*Source` DTO seam (C6) is the abstraction boundary — swapping access modes is additive. The contract is one-way-import-scoped; a future bidirectional sync (WBS Workstream D) is a separate ADR (D0), not an amendment to this one.

---

## ADR-protocol compliance

- **Council requirement (ADR 0069 / 0093):** substrate-tier + real-financial-write → dual-council MANDATORY (sec-eng + .NET-architect) on ADR text AND the A0 PR; test-eng MANDATORY on the data-correctness A-units (H8). `requires-council: [dotnet-architect, security-engineering]` in frontmatter.
- **Composes:** ADR 0084 (sentinel guard for `--target-tenant`), 0088 (Path II + clean-room), 0091 (facade + `IMustHaveTenant`), 0093 (Stage-05), 0097–0099 (substrate cadence + enforcement-invariant discipline).
- **Gates:** A1–A7 build queues on this ADR reaching `Accepted`; A0's design half proceeds in parallel (it recommends the access mode → feeds H-CIC-1). The contract gates A1+ cleanly: every A-unit's council column + the C-clause enforcement invariants are now ratified inputs to the build.
- **Slot:** highest ADR on `shipyard/main` at authoring is 0099 (First-Party Session); 0100 confirmed free by `ls docs/adrs/0100*` (no match).

---

— ONR, 2026-05-29 (Rev 1; awaiting dual-council MANDATORY attestation — security-engineering + .NET-architect)
