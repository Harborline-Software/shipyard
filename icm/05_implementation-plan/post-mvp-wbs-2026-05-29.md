# Post-MVP Work Breakdown Structure — dispatchable PR-sized units (2026-05-29)

**Requester:** CIC (via Admiral dispatch, 2026-05-29)
**Author:** ONR
**Scope:** Decompose ALL post-MVP workstreams into dispatchable PR-sized units. In scope:
the now-ACTIVE ERPNext data importer (Workstream A — import CIC's LIVE local ERPNext into a
Sunfish tenant), the DEFERRED ERPNext-layer retirement (B), the 4 non-PM reference bundles
(C), and lighter-decomposition future items (D–H). For each unit: scope · repo/layer ·
deps/sequence · size · council/SPOT-CHECK requirement · MVP-vs-future tag. Out of scope:
writing production code, authoring the ADRs themselves (this names which units need an ADR
*before* build), prioritization re-rulings (CIC's 2026-05-29 rulings are taken as given).
**Status:** final
**Confidence:** HIGH on what substrate exists (verified on disk + the 3 shipped importer
slices + the merged backlog reality). MEDIUM on sizings (S/M/L are PR-author-effort bands,
not validated against Engineer). The importer's CIC-input prerequisites are HARD blockers —
nothing in Workstream A past A0 can be built without them.

---

## CIC rulings taken as given (2026-05-29)

1. **ERPNext shim/proxy deletion → DEFERRED post-MVP** (was P0 #1). Workstream B below; scoped
   but not launch-blocking.
2. **Non-PM bundles → confirmed post-MVP** (future roadmap). Workstream C below.
3. **ERPNext data importer → PROMOTED TO ACTIVE — build now.** No longer demand-gated: **CIC is
   the customer**; importing CIC's local ERPNext data into Sunfish is the G-1 done-condition
   "the PM business runs on the app." Workstream A below — the most detailed, ordered first.

---

## How to read this WBS

- **Unit ID** — `<workstream>.<n>` (e.g. `A3`, `C1.2`). Dispatch by ID.
- **Size** — `S` ≈ 1 focused PR / <½ day author-effort · `M` ≈ 1 substantive PR / ~1 day ·
  `L` ≈ split into the named sub-PRs (never dispatch an `L` as one PR).
- **Council** — `none` · `sec-eng` (security-engineering SPOT-CHECK) · `test-eng`
  (test-engineering / data-correctness) · `.NET-arch` · `frontend-arch` · `dual` (sec-eng +
  .NET-arch) · `pattern-009 PAIR` (Bridge endpoint + frontend rebind → mandatory sec-eng
  SPOT-CHECK on PR-open per fleet SLA).
- **ADR-before-build** — flagged explicitly where a contract/semantics decision must be
  ratified before the unit is buildable.
- **Tag** — `[ACTIVE]` build-now (Workstream A only) · `[FUTURE]` post-MVP roadmap.

---

## Recommended dispatch order (workstream-level, per CIC priority)

1. **Workstream A — ERPNext data importer** `[ACTIVE]` — build now; A0 first (it is gated on
   CIC supplying instance access). **Bottleneck: A0 can't complete without CIC input — see the
   prerequisites block. Dispatch A0's design half immediately; the rest of A queues behind it.**
2. **Workstream B — ERPNext-layer retirement** `[FUTURE, near-term]` — scoped now, dispatch when
   CIC promotes; the payment+accounting rebind (B1) is the load-bearing prereq and is the only
   part with NEW substrate.
3. **Workstream C — the 4 non-PM bundles** `[FUTURE]` — one mini-WBS each; dispatch per
   bundle-demand signal.
4. **Workstreams D–H — everything else** `[FUTURE]` — incremental sync (D), Bridge.Data entity
   moves (E), ADR 0098 rename wave (F), FED polish DRAFTs (G), hygiene/doc-drift (H).

---

# WORKSTREAM A — ERPNext data importer `[ACTIVE — build now]`

**Goal:** Import CIC's LIVE local ERPNext instance data into a Sunfish tenant so the PM business
runs on Sunfish (G-1 done-condition). Six-pass design per
`_shared/engineering/erpnext-to-anchor-migration-importer-spec.md` (the spec's package-name table
is stale — reconciled below to shipped block names).

### Substrate that ALREADY exists (do NOT re-scope)

Three per-record idempotent upserter slices are shipped on `main` (verified on disk). Each takes
a parsed in-memory ERPNext source record and an idempotency key, returns `ImportOutcome<T>` with
`ImportAction.{Inserted|Updated|Skipped}`:

| Shipped slice | Package | Covers | Importer-spec pass |
|---|---|---|---|
| `IErpnextAccountImporter.UpsertFromErpnextAsync` | `blocks-financial-ledger/Migration/` | Chart-of-accounts upsert (`ErpnextAccountSource` → `GLAccount`) | Pass 1 |
| `IErpnextJournalEntryImporter.UpsertFromErpnextAsync` | `blocks-financial-ledger/Migration/` | Journal-entry upsert (header + lines → `JournalEntry`) | Pass 3 + 4.4 |
| `IErpnextSalesInvoiceImporter.UpsertSalesInvoiceAsync` | `blocks-financial-ar/Migration/` | Sales-invoice upsert (→ `Invoice` + lines) | Pass 4.1 |

Shared primitives also shipped: `ImportOutcome<T>`, `ImportAction`, `InMemoryAccountResolver`,
`ErpnextAccountSource` / `ErpnextSalesInvoiceSource` / `ErpnextJournalEntrySource` records.

**What that means for the WBS:** the per-record write path for chart + JE + sales-invoice is
DONE. What's MISSING is (1) the remaining per-record upserters (parties, AP/bills, payments,
tax, periods, cost-centers), (2) the EXTRACTION adapter (get records OUT of CIC's live instance
into those source records), (3) the ORCHESTRATION driver (run passes in order, tenant-scoped,
with commit boundaries + reject-bin), (4) the RECONCILE pass, (5) the CLI + wizard surface.

### Spec package-name reconciliation (the spec is stale; build against THESE)

| Spec says (stale) | Build against (shipped) |
|---|---|
| `blocks-financial-chart` | `blocks-financial-ledger` (chart folded in; `GLAccount` lives here) |
| `blocks-people-*` (plural cluster) | `blocks-people-foundation` (singular; `Party`/`PartyRole`/`Customer`/`Vendor`) |
| `blocks-financial-budget` | NOT shipped — budget import stays Phase-3/deferred (A-skip) |
| `blocks-property-*` | `blocks-properties` + `blocks-leases` (only if CIC instance has custom DocTypes) |
| `anchor import erpnext` CLI | keep the verb; the host binary is the Sunfish/Anchor runtime CLI (A7) |

### HARD prerequisites from CIC (A0 is blocked until ALL supplied)

**Nothing past A0's design half is buildable until CIC provides:**

1. **Instance access** — ONE of: (a) ERPNext REST API base-URL + API key/secret (read scope);
   (b) a MariaDB dump (`.sql` / mysqldump) of the ERPNext site DB; (c) a per-DocType CSV/JSON
   export bundle (`bench export-fixtures` + per-DocType `export_data`). **ONR recommends (b) the
   MariaDB dump** — rationale in A0; if CIC prefers a different mode, A0 adapts.
2. **DocType inventory** — which ERPNext modules/DocTypes the instance actually uses. Critical
   unknowns: (i) are there CUSTOM `Property` / `Lease` DocTypes, or is `Cost Center` abused as the
   property dimension? (ii) which companies/LLCs are charts (the spec assumes 4: Acero/Bosco/
   Escola/Shirin); (iii) multi-currency present (spec assumes USD-only)?
3. **Target Sunfish tenant** — the tenant-id the import lands into (single-tenant for CIC's import;
   the load path must be tenant-scoped per `IMustHaveTenant` regardless).
4. **Optional reconcile snapshots** — CIC-produced ERPNext-side AR-aging / AP-aging / GL-balance
   snapshots (for Pass 6 verification). If absent, Pass 6 still does the trial-balance invariant
   check but can't cross-check aging totals.

> **Admiral action:** file a CIC-input ask beacon enumerating items 1–3 (4 optional). A0's design
> deliverable can proceed in parallel (it RECOMMENDS the access mode); A1–A6 build queues on the
> answer to item 1; A5 (custom-DocType passes) queues on item 2(i).

---

### A0 — Extraction adapter (the read side) `[ACTIVE]`

| Field | Value |
|---|---|
| **Scope** | Pull ERPNext documents from CIC's live instance into the in-memory `Erpnext*Source` records the upserters consume. Design for all 3 access modes; recommend + implement one. |
| **Repo/layer** | shipyard pkg — NEW `blocks-financial-ledger/Migration/Extraction/` (or a small NEW `blocks-migration-erpnext` host package if the adapter is cross-cluster — A0 design decides) |
| **Deps/sequence** | **Design half = NO deps** (dispatch immediately). **Build half gated on CIC prerequisite #1.** |
| **Size** | `M` (one access mode). `L` if CIC wants all 3 modes shippable. |
| **Council** | **sec-eng** — the adapter holds ERPNext credentials / reads a full DB dump; credential handling + no-secret-logging + read-only-scope review required. |
| **ADR-before-build** | **YES — thin ADR (see A-ADR below).** The extraction contract (which mode, idempotency-version source field, partial-failure semantics) is a substrate contract worth pinning before A1+ depend on it. |
| **Tag** | `[ACTIVE]` |

**Access-mode design (A0 deliverable recommends ONE):**

- **Mode (a) REST API** — `frappe.client.get_list` + `get` per DocType via API key. Pro: live, no
  CIC bench commands. Con: pagination + rate-limit + needs the instance reachable from the import
  host; credential is a live secret. sec-eng-heaviest.
- **Mode (b) MariaDB dump** ⭐ **RECOMMENDED** — CIC runs one `mysqldump`; the adapter reads tables
  directly (read-only, offline, deterministic, re-runnable, no live coupling, no rate-limit). Maps
  ERPNext `tab<DocType>` tables → source records. Pro: matches the spec's "static input, read-only,
  clean-room" design (§1.2/§9); cleanest long-term; one-time CIC action. Con: adapter must know the
  `tab*` table→column shape (still data-only, no Frappe code — clean-room intact per spec §9.1).
- **Mode (c) per-DocType CSV/JSON** — the spec's original assumption (`bench export-fixtures` +
  `export_data`). Pro: spec already designed around it (§2.2 directory layout). Con: most manual
  CIC steps; CSV type-coercion fragility.

**Why (b):** lowest CIC-side friction (one command), offline + deterministic + re-runnable (best
fit for the idempotency contract + dry-run mode), no live-runtime coupling (preserves the spec's
read-only clean-room posture), and it sidesteps REST pagination/rate-limit complexity. Per
`feedback_prefer_cleanest_long_term_option`, (b) is the cleanest. (a) is a fallback if CIC's
instance is remote-only; (c) is the spec's documented path if CIC already has an export.

**A0 must also flag for CIC:** exact `mysqldump` command + which tables (the `tab<DocType>` set
mapping to the spec §3.1 DocType list), produced as the CIC-input ask.

---

### A1 — Pass 1: Chart of Accounts + cost centers `[ACTIVE]`

| Field | Value |
|---|---|
| **Scope** | Orchestrate the SHIPPED `IErpnextAccountImporter` over the full chart: topological parent-first sort, `account_type`→`GLAccountType`/`AccountSubtype` mapping (spec §3.2), cost-center→`Classification`/Property heuristic (spec §3.4). Single transaction per chart. ADD cost-center upserter (not yet shipped). |
| **Repo/layer** | shipyard pkg — `blocks-financial-ledger/Migration/` (account upserter exists; add toposort + cost-center upserter + pass orchestration) |
| **Deps/sequence** | A0 (source records) → A1. First pass; A2+ depend on accounts resolving. |
| **Size** | `M` |
| **Council** | **test-eng** (toposort correctness, cycle detection, account-type mapping coverage). |
| **ADR-before-build** | No (A-ADR covers idempotency/partial-failure; A1 is pure orchestration over a shipped upserter). |
| **Tag** | `[ACTIVE]` |

---

### A2 — Pass 2: Parties (Customer / Supplier / Contact / Address) `[ACTIVE]`

| Field | Value |
|---|---|
| **Scope** | NEW per-record upserters in `blocks-people-foundation` for `Party` + `PartyRole` + `Customer`/`Vendor` extensions + `EmailAddress`/`PhoneNumber`/`PartyAddress` sub-entities (spec §4.2.3). Map `customer_type`→`Party.kind`; attach contacts/addresses via ERPNext `links`. Also Pass-2 reference data: fiscal-years/periods (`blocks-financial-periods`) + tax codes/rates/jurisdictions (`blocks-financial-tax`). |
| **Repo/layer** | shipyard pkg — `blocks-people-foundation/Migration/` (NEW) + `blocks-financial-periods/Migration/` (NEW) + `blocks-financial-tax/Migration/` (NEW) |
| **Deps/sequence** | A0 → A2. Independent of A1 except cost-center→property (A1.cost-center feeds A2.4). Can parallel A1. |
| **Size** | `L` — split: **A2.1** people-foundation party/customer/vendor upserter · **A2.2** periods (fiscal-year/period synthesis) · **A2.3** tax (codes/rates/jurisdictions). |
| **Council** | **sec-eng** on A2.1 — `Party`/`Contact` carry PII (names, emails, phones); audit-log treatment + tier-redacted projection per ADR 0098 S1 PII discipline. **test-eng** on A2.2/A2.3 (period synthesis + tax-rate child-table fan-out). |
| **ADR-before-build** | No (covered by A-ADR). |
| **Tag** | `[ACTIVE]` |

---

### A3 — Pass 3: Opening balances `[ACTIVE]`

| Field | Value |
|---|---|
| **Scope** | Orchestrate the SHIPPED `IErpnextJournalEntryImporter` over `is_opening=="Yes"` JEs (spec §4.3): map to `JournalEntry` with `sourceKind=Migration`, bypass `entryDate<=today`, enforce Σdebit==Σcredit per JE, per-JE transaction, imbalanced→reject-bin. |
| **Repo/layer** | shipyard pkg — `blocks-financial-ledger/Migration/` (JE upserter exists; add opening-filter + balance-gate orchestration) |
| **Deps/sequence** | A1 (accounts must resolve) → A3. |
| **Size** | `S` (thin orchestration over the shipped JE upserter). |
| **Council** | **test-eng** (balance invariant; back-dated entryDate handling). |
| **ADR-before-build** | No. |
| **Tag** | `[ACTIVE]` |

---

### A4 — Pass 4: Transactional history `[ACTIVE]`

| Field | Value |
|---|---|
| **Scope** | The bulk transactional stream (spec §4.4), strict sub-pass order: **4.1 Sales Invoices** (shipped upserter — orchestrate + de-itemize) · **4.2 Purchase Invoices → Bills** (NEW upserter in `blocks-financial-ap`) · **4.3 Payment Entries → Payment+PaymentApplication** (NEW upserter in `blocks-financial-payments`; `payment_type`→direction, `mode_of_payment`→method, reference child-rows→applications) · **4.4 Standalone JEs** (shipped upserter — orchestrate the non-opening / non-derived set). Per-record transaction; per-record reject→bin. |
| **Repo/layer** | shipyard pkg — `blocks-financial-ar/Migration/` (4.1 exists) + `blocks-financial-ap/Migration/` (NEW, 4.2) + `blocks-financial-payments/Migration/` (NEW, 4.3) + `blocks-financial-ledger/Migration/` (4.4 exists) |
| **Deps/sequence** | A1 + A2 (accounts + parties) → A4. Sub-passes 4.1→4.2→4.3→4.4 are strictly ordered (payments reference invoices/bills). |
| **Size** | `L` — split: **A4.2** AP/Bill upserter + orchestration · **A4.3** Payment + PaymentApplication upserter + orchestration · **A4.1+A4.4** orchestration over shipped upserters (one PR). |
| **Council** | **test-eng** MANDATORY on A4.2 + A4.3 (financial correctness: bill de-itemize, payment-application allocation sums must equal source `allocated_amount`). **sec-eng** light (tenant-scope on the write path). |
| **ADR-before-build** | No (A-ADR covers the contract; these are upserters + orchestration). |
| **Tag** | `[ACTIVE]` |

---

### A5 — Pass 5 prep + custom PM DocTypes (Property / Unit / Lease) `[ACTIVE — CONDITIONAL]`

| Field | Value |
|---|---|
| **Scope** | (a) Reconciliation-linkage pass (spec §4.5): heuristic-match unapplied payments to invoices/bills by party+amount+date. (b) **CONDITIONAL on CIC prereq #2(i):** if CIC's instance has custom `Property`/`Lease` DocTypes, NEW upserters in `blocks-properties` + `blocks-leases`; else cost-center→property heuristic (A1) + manual entry covers it (NO unit built). |
| **Repo/layer** | shipyard pkg — `blocks-financial-payments/Migration/` (5a linkage) + `blocks-properties/Migration/` + `blocks-leases/Migration/` (5b, conditional) |
| **Deps/sequence** | A4 (payments + invoices exist) → A5a. A5b independent (parties optional). |
| **Size** | `S` (5a linkage) + `M` (5b, only if custom DocTypes exist). |
| **Council** | **test-eng** (linkage heuristic ambiguity handling). |
| **ADR-before-build** | No. **Gate: confirm prereq #2(i) before scoping A5b.** |
| **Tag** | `[ACTIVE]` (A5b conditional) |

---

### A6 — Pass 6: Reconcile + verify `[ACTIVE]`

| Field | Value |
|---|---|
| **Scope** | The correctness gate (spec §4.6 / §7.2): per-chart trial-balance (Σdebit−Σcredit==0, hard zero); AR/AP aging diff vs CIC snapshots (±$0.01); per-account balance diff; invoice-balance reconciliation; **emit `migration-report.md`** (7 sections: run summary, trial-balance, AR/AP aging, per-account diff, reject-bin, unapplied payments, cost-center resolution). Read-only pass. |
| **Repo/layer** | shipyard pkg — NEW `Migration/Reconciliation/` (cross-cluster reader; lives in the host package or `blocks-reports/Migration/`) |
| **Deps/sequence** | A1–A5 (all data landed) → A6. |
| **Size** | `M` |
| **Council** | **test-eng** MANDATORY (this IS the data-correctness pass; the acceptance gate for "did the import preserve CIC's books"). |
| **ADR-before-build** | No (A-ADR's verify-semantics section covers tolerance + halt-on-mismatch). |
| **Tag** | `[ACTIVE]` |

---

### A7 — Driver: CLI + dry-run + report mode `[ACTIVE]`

| Field | Value |
|---|---|
| **Scope** | The orchestration entry point: `anchor import erpnext --source <ref> --target-chart <tenant>` (spec §8). Flags: `--dry-run` (in-memory SQLite, report only, no writes), `--from-pass N` (resume), `--allow-aging-drift`, `--reject-threshold N`, `--verbose`. Tenant-scoped target. Idempotent re-run (skip-by-version per shipped `ImportOutcome`). Progress UX (per-pass status lines). |
| **Repo/layer** | shipyard pkg — host CLI binary (Sunfish/Anchor runtime CLI surface; NEW `Migration/Cli/` or a small console host project) |
| **Deps/sequence** | A1–A6 → A7 (the CLI wires the passes). |
| **Size** | `M` |
| **Council** | **sec-eng** (tenant-isolation on the load path: the CLI must NOT cross-tenant-write; `--target-chart` must be validated tenant-scoped) + **test-eng** (dry-run vs commit equivalence; re-run idempotency C1/C2/C3 acceptance). |
| **ADR-before-build** | No (A-ADR covers partial-failure/resume semantics). |
| **Tag** | `[ACTIVE]` |

---

### A8 — Admin import wizard (optional, post-CLI) `[ACTIVE — defer to after A7]`

| Field | Value |
|---|---|
| **Scope** | Tauri-React onboarding wizard wrapping the A7 CLI (spec §8.3): step 1 point-at-source; step 2 dry-run preview (renders the 7-section report inline); step 3 commit; step 4 success + link to report. Same import logic as A7 (CLI is canonical). |
| **Repo/layer** | sunfish web (React) + Tauri shell + signal-bridge (a thin import-trigger + report-fetch endpoint pair) |
| **Deps/sequence** | A7 (CLI is the engine) → A8. |
| **Size** | `M` (it's a thin wrapper; the engine is A7). |
| **Council** | **pattern-009 PAIR** (Bridge import-trigger/report endpoint + frontend rebind → sec-eng SPOT-CHECK on PR-open) + **sec-eng** (the wizard initiates a tenant-scoped bulk write; auth + tenant-scope on the trigger endpoint). |
| **ADR-before-build** | No. |
| **Tag** | `[ACTIVE]` — but CIC's own import can run via A7 CLI; A8 is for the eventual customer-facing path. Lowest priority within A. |

---

### A-ADR — Importer contract ADR (thin) `[ACTIVE — BEFORE A1+ build]`

| Field | Value |
|---|---|
| **Scope** | A THIN ADR pinning the cross-cutting importer contract that A1–A7 all depend on: (1) **idempotency contract** (externalRef `{source,id,version}` key; re-run = insert/update/skip-by-`modified`; posted-JE immutability → update-forbidden warning); (2) **partial-failure semantics** (per-pass commit boundaries per spec §6.2; reject-bin; resume-from-pass); (3) **tenant-scope invariant** (every write `IMustHaveTenant`; `--target-chart` validation); (4) **clean-room posture** (data-format-only read per spec §9; attribution header). Folds the spec's 10 open questions (§10) into ratified decisions. |
| **Repo/layer** | shipyard `docs/adrs/` (next free number) |
| **Deps/sequence** | Author + ratify BEFORE A1 build begins (A0 design can proceed in parallel; A0's extraction contract feeds the ADR). |
| **Size** | `S`–`M` (thin ADR; the spec already did the heavy design — this ratifies the contract + resolves §10 open questions). |
| **Council** | **dual (sec-eng + .NET-arch)** — substrate-tier contract; sec-eng on tenant-isolation + credential + clean-room; .NET-arch on the upserter/orchestration contract + commit-boundary model. |
| **Tag** | `[ACTIVE]` |

> **Why an ADR is warranted (per directive ask):** the importer crosses 7 packages, writes the
> financial substrate, and holds CIC's real books — the idempotency + partial-failure + tenant-scope
> + clean-room contract is exactly the kind of substrate-tier decision the fleet ratifies before
> build. The spec is a Stage-03 design doc, not a ratified contract; A-ADR is the ratification. Keep
> it THIN — the spec carries the detail; the ADR pins the load-bearing decisions + closes §10.

### Workstream A roll-up

**Unit count: 9 build units (A0–A8) + 1 ADR (A-ADR) = 10.** Of these, **3 upserters already
shipped** (chart/JE/sales-invoice per-record write paths). New build: extraction adapter (A0),
4 new upserters (cost-center, parties/periods/tax, AP/Bill, Payment), pass orchestration (A1/A3/
A4.1/A4.4), reconcile (A6), CLI (A7), wizard (A8). **Critical path:** A-ADR + A0-design →
A0-build (CIC-gated) → A1 → A2 → A3 → A4 → A5 → A6 → A7 → [A8]. **Dispatch A-ADR + A0-design NOW;
everything else queues on CIC prereq #1.**

---

# WORKSTREAM B — ERPNext-layer retirement `[FUTURE — deferred post-MVP per CIC]`

**Goal:** Delete the ERPNext shim + Bridge proxy after rebinding the load-bearing consumers off it.
Decomposes the cohort-4 6-PR deletion plan
(`_shared/design/cohort-4/02-erpnext-deletion-strategy.md`). **The payment+accounting rebind (B1/B2)
is the load-bearing prereq — it has NEW substrate; everything after is mechanical deletion.**

State on main (verified): `apps/web/src/api/erpnext.ts` LIVE with ~9 consumers (incl. legacy
`RentRoll.tsx`/`PLReport.tsx`/`AccountingPage.tsx` + 4 test files); signal-bridge `ERPNextProxy.cs`
+ `IERPNextClient.cs` + `ERPNextHttpClient.cs` + `ERPNextOptions.cs` LIVE.

### B1 — Payment rebind (Bridge endpoint + FED rebind PAIR) `[FUTURE]`

| Field | Value |
|---|---|
| **Scope** | NEW `getPayments(leaseId)` + `recordPayment(leaseId, payload)` in `@/api/payments` → NEW Bridge `GET`/`POST /api/v1/leases/{leaseId}/payments`. Migrate `useLeases.ts` + `RentCollectionPage.tsx` off `@/api/erpnext`. (cohort-2 RB-8 never landed — this is the deferred load-bearing half.) |
| **Repo/layer** | signal-bridge Bridge (endpoint pair) + sunfish web (rebind) |
| **Deps/sequence** | FIRST in B — deletion can't proceed while payment is on the proxy. |
| **Size** | `M` (2 PRs — Bridge + FED, paired). |
| **Council** | **pattern-009 PAIR** — sec-eng SPOT-CHECK MANDATORY on PR-open. |
| **ADR-before-build** | No. |
| **Tag** | `[FUTURE]` |

### B2 — Accounting page rebind OR retirement `[FUTURE — DECISION first]`

| Field | Value |
|---|---|
| **Scope** | `AccountingPage.tsx` still calls `getAccountingSummary`/`getAccountingOutstanding` off the proxy. EITHER rebind to NEW `@/api/accounting` + Bridge endpoints, OR delete the page (cohort-3 TrialBalancePage + ArAgingPage may cover the surface). |
| **Repo/layer** | signal-bridge + sunfish web (rebind) OR sunfish web only (delete) |
| **Deps/sequence** | After B1. **Needs a rebind-vs-delete DECISION (cohort-4 Q1) before scoping.** |
| **Size** | `M` (rebind, pattern-009 pair) or `S` (delete page+route+nav+test). |
| **Council** | **pattern-009 PAIR** if rebind (sec-eng); **none** if delete. |
| **ADR-before-build** | No — but a CIC/Admiral rebind-vs-delete ruling gates it. |
| **Tag** | `[FUTURE]` |

### B3 — Legacy route audit + retirement (RentRoll.tsx / PLReport.tsx) `[FUTURE — conditional]`

| Field | Value |
|---|---|
| **Scope** | Verify whether legacy `RentRoll.tsx` + `PLReport.tsx` are still mounted (cohort-3 shipped NEW `RentRollPage.tsx`/`ProfitAndLossByPropertyPage.tsx`). If mounted: remove routes + nav + delete legacy pages + migrate test assertions. If already unmounted: skip. |
| **Repo/layer** | sunfish web |
| **Deps/sequence** | After B2; before B4 (these consume `getRentRoll`/`getProfitLoss` off the shim). |
| **Size** | `S` (mechanical; conditional on audit). |
| **Council** | none. |
| **ADR-before-build** | No. |
| **Tag** | `[FUTURE]` |

### B4 — `erpnext.ts` deletion + test-import migration `[FUTURE]`

| Field | Value |
|---|---|
| **Scope** | Delete `apps/web/src/api/erpnext.ts`; migrate the 4 test files (`LeaseDetailPage.test.tsx`, `MaintenancePage.test.tsx`, `RentRoll.test.tsx`, `PLReport.test.tsx`, `LeasesPage.test.tsx`) off ERPNext-shape fixtures to cartridge-shape; clean the `properties.ts` historical comment; update sunfish `.wolf/anatomy.md`. Acceptance: zero `@/api/erpnext` + zero `/api/v1/erpnext` grep hits in `apps/web/src/`. |
| **Repo/layer** | sunfish web |
| **Deps/sequence** | After B1+B2+B3 (all consumers rebound/retired). |
| **Size** | `M` (deletion + test migration). |
| **Council** | none (mechanical; the rebinds carried the risk). |
| **ADR-before-build** | No. |
| **Tag** | `[FUTURE]` |

### B5 — Config/env cleanup `[FUTURE]`

| Field | Value |
|---|---|
| **Scope** | Remove ERPNext-named env vars (`.env.example`), Bridge ERPNext upstream-URL config (`ERPNextOptions`), any admin-panel ERPNext settings. Bundle with B4 if trivial. |
| **Repo/layer** | sunfish web + signal-bridge config |
| **Deps/sequence** | With/after B4. |
| **Size** | `S` |
| **Council** | none. |
| **ADR-before-build** | No. |
| **Tag** | `[FUTURE]` |

### B6 — Bridge `/api/v1/erpnext/*` route + proxy family deletion `[FUTURE — LAST]`

| Field | Value |
|---|---|
| **Scope** | Delete `ERPNextProxy.cs` + `IERPNextClient.cs` + `ERPNextHttpClient.cs` + `ERPNextOptions.cs` + the `/api/v1/erpnext/*` route registrations + `ERPNextProxyTests.cs`. MUST be last — Bridge can't drop routes until ALL FED consumers are off them (else mid-deploy 404s). |
| **Repo/layer** | signal-bridge Bridge |
| **Deps/sequence** | LAST — after B1–B5 (all consumers rebound + FED deletion shipped). |
| **Size** | `M` |
| **Council** | **sec-eng** (removing a route family — confirm no orphaned auth/audit middleware; the deletion REDUCES attack surface, which is the point). |
| **ADR-before-build** | No. |
| **Tag** | `[FUTURE]` |

### Workstream B roll-up

**Unit count: 6 (B1–B6).** Strict sequence: B1 → B2 → B3 → B4 → B5 → B6. Only B1 (+B2-if-rebind)
has NEW substrate (pattern-009 pairs, sec-eng); B3–B6 are mechanical retirement. Candidate
`pattern-018-shim-retirement-finale` (per cohort-4 doc) ratifies on the 2nd fleet shim-retirement.

---

# WORKSTREAM C — the 4 non-PM reference bundles `[FUTURE]`

**Goal:** Stand up a UI cockpit + bundle activation for each of the 4 Draft-manifest bundles. Each
is a cohort-shaped mini-WBS: substrate gap-fill → Bridge endpoints → React cockpit → pattern-009
pairs → manifest activation. **Substrate that already exists is NOT re-scoped.**

**Manifest module-key → shipped-package reality (the manifests use aspirational keys; map first):**

| Manifest key (aspirational) | Shipped package (reality) | Built? |
|---|---|---|
| `sunfish.blocks.workflow` | `blocks-workflow` | YES |
| `sunfish.blocks.forms` | `blocks-forms` | YES |
| `sunfish.blocks.tasks` | `blocks-tasks` | YES (6 .cs — thin) |
| `sunfish.blocks.scheduling` | `blocks-scheduling` | YES |
| `sunfish.blocks.assets` | `blocks-assets` | YES (partial) |
| `sunfish.blocks.maintenance` | `blocks-maintenance` | YES |
| `sunfish.blocks.inspections` | `blocks-inspections` | YES (35 .cs) |
| `sunfish.blocks.projects` | `blocks-work-projects` (85 .cs) | YES |
| `sunfish.blocks.crm` | — (capability in `blocks-people-foundation` PartyRole) | PARTIAL — no dedicated CRM pipeline UI |
| `sunfish.blocks.contacts` | `blocks-people-foundation` | YES (substrate) |
| `sunfish.blocks.diligence` | `blocks-businesscases` (18 .cs) | PARTIAL |
| `sunfish.blocks.documents` | `blocks-docs` (40 .cs) | YES |
| `sunfish.blocks.reporting` | `blocks-reports` (52 .cs) | YES |
| `sunfish.blocks.reservations` | — | NOT BUILT |
| `sunfish.blocks.procurement` | — | NOT BUILT |
| `sunfish.blocks.vendors` | `blocks-people-foundation` (Vendor extension) | YES (substrate) |

**Cross-cutting C-pattern (applies to all 4):** the substrate is mostly built; the GAP is the
**UI cockpit** (React pages) + **Bridge endpoints** to surface it + **manifest activation**
(`Draft`→`Active` + module-key reconciliation). Each bundle's cockpit page that reads/writes a
block needs a pattern-009 Bridge+frontend pair.

---

### C1 — Asset-Management bundle `[FUTURE]`

Required modules (manifest): workflow, forms, tasks, scheduling, **assets**, maintenance,
inspections — all substrate-shipped. Gap: asset-lifecycle cockpit UI + asset Bridge endpoints.

| Unit | Scope | Repo/layer | Deps | Size | Council | Tag |
|---|---|---|---|---|---|---|
| **C1.1** | Substrate gap-fill: `blocks-assets` lifecycle/depreciation completeness audit (it's "partial" per register) — fill missing asset-lifecycle + warranty entities to satisfy `featureDefaults` (lifecycle.tracking, depreciation, warrantyReminders) | shipyard pkg `blocks-assets` | none | `M` | test-eng | `[FUTURE]` |
| **C1.2** | Asset Bridge endpoints: `GET/POST/PUT /api/v1/assets`, `/assets/{id}`, lifecycle transitions | signal-bridge | C1.1 | `M` | sec-eng (tenant-scope) | `[FUTURE]` |
| **C1.3** | Asset cockpit React pages: asset list/detail, lifecycle timeline, warranty reminders, maintenance-link | sunfish web | C1.2 | `L` (list+detail+lifecycle = 2-3 PRs) | **pattern-009 PAIR** w/ C1.2 (sec-eng) | `[FUTURE]` |
| **C1.4** | Bundle activation: `asset-management.bundle.json` `Draft`→`Active` + reconcile module keys to shipped names | shipyard pkg foundation-catalog | C1.1–C1.3 | `S` | none | `[FUTURE]` |

### C2 — Project-Management bundle `[FUTURE]`

Required: workflow, forms, tasks, scheduling — shipped. Substrate `blocks-work-projects` (85 .cs)
is the richest non-PM block. Gap: project cockpit UI (work-orders surface exists; full PM cockpit
does not) + optional CRM pipeline + Gantt (FED already shipped a Gantt MVP — reuse).

| Unit | Scope | Repo/layer | Deps | Size | Council | Tag |
|---|---|---|---|---|---|---|
| **C2.1** | Substrate gap-fill: `blocks-work-projects` cockpit-readiness audit (Gantt/budget-tracking/task-deps per `featureDefaults`) — mostly built; confirm + fill | shipyard pkg `blocks-work-projects` | none | `S`–`M` | test-eng | `[FUTURE]` |
| **C2.2** | Project Bridge endpoints: `/api/v1/projects`, `/projects/{id}`, tasks, milestones, budget-lines | signal-bridge | C2.1 | `M` | sec-eng | `[FUTURE]` |
| **C2.3** | Project cockpit React pages: project list/detail, Gantt (reuse FED Gantt MVP sunfish#22), task board, budget tracker | sunfish web | C2.2 | `L` (3 PRs) | **pattern-009 PAIR** (sec-eng + frontend-arch on Gantt) | `[FUTURE]` |
| **C2.4** | Optional CRM pipeline UI (manifest `crm.pipelineStages`) — only if PM bundle wants lead intake; else skip | sunfish web + signal-bridge | C2.2 | `M` | pattern-009 PAIR | `[FUTURE]` |
| **C2.5** | Bundle activation: `project-management.bundle.json` `Draft`→`Active` + module-key reconcile | shipyard pkg foundation-catalog | C2.1–C2.3 | `S` | none | `[FUTURE]` |

### C3 — Facility-Operations bundle `[FUTURE]`

Required: workflow, forms, tasks, scheduling, **maintenance, inspections, assets** — all shipped
(this bundle reuses the most PM-vertical substrate). Gap: facility cockpit + reservations (NOT
built) + cross-facility work-order intake.

| Unit | Scope | Repo/layer | Deps | Size | Council | Tag |
|---|---|---|---|---|---|---|
| **C3.1** | Substrate gap-fill: NEW `blocks-reservations` (bookable-space substrate — manifest requires `reservations.conflictDetection`; NOT built) | shipyard pkg NEW `blocks-reservations` | none | `M`–`L` | **dual** (new substrate package) | `[FUTURE]` |
| **C3.2** | Facility Bridge endpoints: facility work-order intake (multi-channel per `featureDefaults`), reservations, SLA tracking | signal-bridge | C3.1 + existing maintenance | `M` | sec-eng | `[FUTURE]` |
| **C3.3** | Facility cockpit React pages: facility dashboard, work-order intake board, reservation calendar, inspection scheduler | sunfish web | C3.2 | `L` (3 PRs) | **pattern-009 PAIR** (sec-eng) | `[FUTURE]` |
| **C3.4** | Bundle activation: `facility-operations.bundle.json` `Draft`→`Active` + module-key reconcile | shipyard pkg foundation-catalog | C3.1–C3.3 | `S` | none | `[FUTURE]` |

### C4 — Acquisition / Underwriting bundle `[FUTURE]`

Required: workflow, forms, tasks — shipped. Substrate `blocks-businesscases` (18 .cs) + the Q6
tender-side bundle manifest exist; gap: Sunfish-side diligence cockpit + data-room + CRM deal
pipeline + approval gates. NOTE: this bundle is `lite`-unsupported (data-room/audit needs exceed
local-first) — `SelfHosted`/`HostedSaaS` only.

| Unit | Scope | Repo/layer | Deps | Size | Council | Tag |
|---|---|---|---|---|---|---|
| **C4.1** | Substrate gap-fill: `blocks-businesscases` diligence-checklist + approval-gate + evidence completeness (manifest `diligence.evidenceRequired`/`approvalGates`); data-room on `blocks-docs` (`documents.dataRoom`/watermarking/external-access-audit) | shipyard pkg `blocks-businesscases` + `blocks-docs` | none | `L` (diligence + data-room = 2 PRs) | **dual** (diligence approval-gates + data-room external-access = sec-eng access-control + audit) | `[FUTURE]` |
| **C4.2** | Acquisition Bridge endpoints: deal pipeline, diligence checklists, data-room access (audit-logged), approval workflow | signal-bridge | C4.1 | `M`–`L` | **sec-eng** MANDATORY (data-room external access + audit trail = security-critical) | `[FUTURE]` |
| **C4.3** | Acquisition cockpit React pages: deal pipeline (Kanban), diligence checklist, data-room viewer, approval-gate UI | sunfish web | C4.2 | `L` (3 PRs) | **pattern-009 PAIR** (sec-eng MANDATORY — external-counsel/investor access) | `[FUTURE]` |
| **C4.4** | Bundle activation: `acquisition-underwriting.bundle.json` `Draft`→`Active` + module-key reconcile (+ coordinate w/ Q6 tender-side manifest) | shipyard pkg foundation-catalog | C4.1–C4.3 | `S` | none | `[FUTURE]` |

### Workstream C roll-up

**Unit count: 18 (C1: 4, C2: 5, C3: 4, C4: 4 — one C4.1 splits to 2 + one C2.4 optional).** Two
NEW substrate packages surface: `blocks-reservations` (C3.1) + the diligence/data-room fill (C4.1)
— both `dual`-council. C4 (acquisition) is the security-heaviest (data-room external access).
**Dispatch order within C: C3 first** (reuses the most existing substrate — lowest gap), then C1,
C2, C4 (highest substrate gap + security surface). Each bundle is independently dispatchable.

> **PM bundle manifest note (belongs in H, flagged here):** the EXISTING property-management
> bundle is feature-complete but its manifest still says `Draft` + lists non-existent module keys
> — flip to `Active` + reconcile as part of H (hygiene), independent of C.

---

# WORKSTREAM D — ERPNext incremental bidirectional sync (Scope B) `[FUTURE — far]`

**Goal:** Keep a small ERPNext shard in sync with Sunfish during a phased cutover (the spec's v1.2
"incremental import" + a write-back path). A ~3-6mo workstream IF demanded — not decomposed to
PR-level here (premature; the contract isn't designed). Lighter decomposition:

| Unit | Scope | Repo/layer | Size | Council | Tag |
|---|---|---|---|---|---|
| **D0** | ADR: bidirectional sync contract (conflict resolution, source-of-truth-per-DocType, delta detection, write-back safety) — **REQUIRED before any build** | shipyard `docs/adrs/` | `M`–`L` | dual | `[FUTURE]` |
| **D1** | Delta-detection (incremental read by `modified` watermark — extends A0 extraction) | shipyard pkg | `M` | test-eng | `[FUTURE]` |
| **D2** | Write-back path (Sunfish→ERPNext; the spec's §1.2 explicitly EXCLUDED this — net-new) | shipyard pkg | `L` | dual | `[FUTURE]` |
| **D3** | Conflict resolution + reconciliation loop | shipyard pkg | `L` | test-eng | `[FUTURE]` |

**Note:** D is NOT actionable until a customer demands a phased dual-run. Build A (one-way import)
first; D only if the one-way cutover proves insufficient. D0 ADR gates everything.

---

# WORKSTREAM E — Bridge.Data PM-leakage entity moves + ADR 0015 `[FUTURE — pre-1.0, low urgency]`

**Goal:** Move PM-vertical entities out of `Sunfish.Bridge.Data` into their proper blocks (per
`_shared/engineering/bridge-data-audit.md` M0–M5). Pre-1.0 internal cleanup; deepens the
"Bridge≡PM" anti-assumption every week it sits. NOT launch-blocking.

| Unit | Scope | Repo/layer | Deps | Size | Council | Tag |
|---|---|---|---|---|---|---|
| **E0 (M0)** | ADR 0015 — module-entity-registration into Bridge DbContext (option-1 vs option-2; single-DbContext composition pattern) — **REQUIRED before any move** | shipyard `docs/adrs/0015` | none | `M` | .NET-arch | `[FUTURE]` |
| **E1 (M1)** | Move `TaskItem`/`TaskStatus`/`TaskPriority`/`Subtask` + permissions → `blocks-tasks`; migration: table-rename w/ block prefix | signal-bridge + shipyard `blocks-tasks` | E0 | `M` | .NET-arch (migration safety) | `[FUTURE]` |
| **E2 (M2)** | Create `blocks-projects` (NEW) — move `Project`/`ProjectMember`/`Milestone`/`Risk`/`BudgetLine` (NOTE: may overlap `blocks-work-projects` — reconcile first) | signal-bridge + shipyard | E0, E1 | `L` | .NET-arch | `[FUTURE]` |
| **E3 (M3)** | Comments decision — `Comment` → `blocks-messaging`/`blocks-crew-comms` or stay task-local | signal-bridge + shipyard | E1 | `S` | none | `[FUTURE]` |
| **E4 (M4)** | Budget/accounting split — `BudgetLine` final home (financial cluster vs projects) | signal-bridge + shipyard | E2 | `M` | .NET-arch | `[FUTURE]` |
| **E5 (M5)** | `AuditRecord` final home — confirm shell-level (likely stays in Bridge) | signal-bridge | E0 | `S` | sec-eng (audit substrate) | `[FUTURE]` |

**Note:** each move is a DB-migration event (table rename, DAB-config update). E0 ADR gates all
moves. E2's `blocks-projects` likely overlaps the already-shipped `blocks-work-projects` (85 .cs) —
**reconcile before creating a duplicate** (this is a real risk flagged in the audit; the WBS surfaces
it). Move-without-target-block-first = orphaned-code anti-pattern (audit Risk).

---

# WORKSTREAM F — ADR 0098 Steps 3-7 block renames `[FUTURE — deferred per CIC 2026-05-29]`

**Goal:** Complete the block-naming generalization rename wave. Steps 1-2 LANDED (foundation-
agreements shipyard#168; rent-collection→recurring-billing shipyard#172). Steps 3-7 DEFERRED
post-MVP per CIC ruling 2026-05-29 — the corrected mechanism (Revision 3: `TypeForwardedTo` does
NOT forward renamed types) makes each rename a fleet-wide ~200-ref downstream-churn atomic bundle.

| Unit | Scope | Repo/layer | Deps | Size | Council | Tag |
|---|---|---|---|---|---|---|
| **F3** | Rename `blocks-work-orders` → canonical (work-items) + atomic downstream consumer-update PRs (sunfish + signal-bridge) in the SAME wave | shipyard + sunfish + signal-bridge | none | `L` (rename + ~N downstream refs atomic) | .NET-arch | `[FUTURE]` |
| **F4** | Rename `blocks-inspections` → canonical + atomic downstream | shipyard + consumers | none | `L` | .NET-arch | `[FUTURE]` |
| **F5** | Rename `blocks-public-listings` → canonical + atomic downstream | shipyard + consumers | none | `L` | .NET-arch | `[FUTURE]` |
| **F6** | Renames #6 (`LeaseOfferId`→`OfferTermsId` etc. per ADR 0098 A2 cross-ref table) + FHA/FCRA-surface generalization | shipyard + consumers | none | `L` | **sec-eng** (S5: FHA+FCRA compliance surface — DemographicProfile/AdverseActionNotice/BackgroundCheckResult) | `[FUTURE]` |
| **F7** | `SUNFISH_BLOCKDEP001` Roslyn deprecation analyzer (`foundation-block-naming.analyzers`) + Step 7b ESLint cross-language rule | shipyard pkg NEW analyzer | none (but most useful BEFORE F3-F6 to guide stale-ref detection) | `M` | .NET-arch | `[FUTURE]` |

**Note:** ADR 0098 already Accepted (Rev 3 correction); these are IMPLEMENTATION steps, no new ADR.
Each rename = honest hard-break + atomic downstream PR bundle (Rev 3 Option A). **Dispatch F7 FIRST**
(the analyzer guides the rename-wave stale-ref detection). Each rename is a discrete dispatchable
`L` bundle. Low priority — platform-reuse investment, not MVP.

---

# WORKSTREAM G — FED post-MVP polish DRAFTs `[FUTURE — PARKED]`

**Goal:** Finish the 5 OPEN FED launch-polish DRAFTs (correctly parked post-MVP). One unit each;
all sunfish web; all FED-owned; no new substrate.

| Unit | Scope | PR | Size | Council | Tag |
|---|---|---|---|---|---|
| **G1** | Dark-mode theme completion | sunfish#31 | `M` | frontend-arch (light) | `[FUTURE]` |
| **G2** | i18n (react-i18next) wiring + locale extraction | sunfish#32 | `M` | none | `[FUTURE]` |
| **G3** | WCAG 2.2 AA audit + remediation | sunfish#35 | `M` | frontend-arch (a11y) | `[FUTURE]` |
| **G4** | Keyboard-shortcut system | sunfish#37 | `S`–`M` | none | `[FUTURE]` |
| **G5** | OpenFeature feature-flags integration | sunfish#38 | `M` | frontend-arch (flag-eval correctness) | `[FUTURE]` |

**Note:** each is an existing DRAFT — finish + Ready-flip, not net-new authoring. Independently
dispatchable to FED in any order. Low priority (polish, not feature-complete-blocking).

---

# WORKSTREAM H — Hygiene / doc-drift `[FUTURE — keep the project legible]`

**Goal:** Mechanical hygiene + doc-drift refresh. NOT feature work; routes to on-demand Sonnet
subagents. One unit each.

| Unit | Scope | Repo/layer | Size | Council | Tag |
|---|---|---|---|---|---|
| **H1** | Phase-3 namespace rename (#44 — plan drafted, awaiting quiescence) | shipyard/sunfish | `M` | .NET-arch | `[FUTURE]` |
| **H2** | A2 ADR-0097 corrigendum (doc correction) | shipyard `docs/adrs/0097` | `S` | none | `[FUTURE]` |
| **H3** | C6 `Auth.*` audit-registry promotion (pattern-lifecycle bookkeeping) | shipyard | `S` | sec-eng (light, audit-registry) | `[FUTURE]` |
| **H4** | Doc-drift: banner `roadmap-tracker.md` SUPERSEDED + refresh `MASTER-PLAN.md` + `active-workstreams.md` (last real update 2026-05-06/19) | shipyard `_shared`/`icm/_state` | `M` | none | `[FUTURE]` |
| **H5** | Property-Mgmt bundle manifest `Draft`→`Active` + reconcile module keys to shipped package names (drift #3 from register) | shipyard pkg foundation-catalog | `S` | none | `[FUTURE]` |
| **H6** | Reconcile-or-close stale tasks: #88 (pattern-009 SLA codified), #145 (CreateWorkOrderForm migration), #176 (cohort-4 activation) | bookkeeping | `S` | none | `[FUTURE]` |
| **H7** | AP-Aging report page + `ApAgingSummaryCartridge` (mechanical mirror of shipped AR-aging; `ApAgingService` substrate exists; closes PM reports suite) | shipyard + sunfish | `M` (~5-7h) | pattern-009 PAIR (light — report cartridge + page) | `[FUTURE]` (MVP close-out per backlog P1#5 — could promote) |

**Note:** H7 (AP-Aging) is tagged future here but the backlog lists it as P1 MVP close-out — **flag
to Admiral: H7 may belong in the MVP-close-out lane, not post-MVP.** The rest are genuine hygiene.

---

# Total unit counts by workstream

| WS | Workstream | Units | NEW substrate? | Tag |
|---|---|---|---|---|
| **A** | ERPNext data importer | **10** (A0–A8 + A-ADR) | 4 new upserters + extraction + reconcile + CLI (3 upserters SHIPPED) | `[ACTIVE]` |
| **B** | ERPNext-layer retirement | **6** (B1–B6) | only B1/B2 (payment/accounting rebind) | `[FUTURE]` |
| **C** | 4 non-PM bundles | **18** (C1:4, C2:5, C3:4, C4:4+split) | `blocks-reservations` (C3.1) + diligence/data-room fill (C4.1) | `[FUTURE]` |
| **D** | Incremental bidirectional sync | **4** (D0–D3) | D0 ADR + write-back path | `[FUTURE — far]` |
| **E** | Bridge.Data entity moves | **6** (E0–E5) | E0 ADR 0015 + `blocks-projects` (reconcile w/ work-projects!) | `[FUTURE]` |
| **F** | ADR 0098 rename wave | **5** (F3–F7) | F7 analyzer | `[FUTURE]` |
| **G** | FED polish DRAFTs | **5** (G1–G5) | none (finish existing DRAFTs) | `[FUTURE]` |
| **H** | Hygiene / doc-drift | **7** (H1–H7) | none | `[FUTURE]` |
| | **TOTAL** | **61 units** | | |

# Units that need an ADR BEFORE build

1. **A-ADR** (importer contract — idempotency / partial-failure / tenant-scope / clean-room) — THIN;
   `[ACTIVE]`; **gates A1+ build** (A0 design proceeds in parallel). **Dispatch first within A.**
2. **D0** (bidirectional sync contract) — `[FUTURE]`; gates all of D.
3. **E0 / ADR 0015** (module-entity-registration) — `[FUTURE]`; gates all Bridge.Data moves.
4. F (ADR 0098) is ALREADY Accepted — F3-F7 are implementation, NO new ADR.

# Recommended dispatch order (unit-level, by CIC priority)

1. **A-ADR (author+ratify) + A0-design** — IN PARALLEL, NOW. A0-design recommends the access mode
   → feeds the CIC-input ask.
2. **Admiral: file CIC-input ask** (instance access mode + DocType inventory + target tenant).
3. **A0-build → A1 → A2 → A3 → A4 → A5 → A6 → A7** — the importer critical path, once CIC supplies
   access. (A8 wizard last, optional.)
4. **B1 (+B2 decision)** — when CIC promotes ERPNext retirement; the only B units with new substrate.
5. **C3 → C1 → C2 → C4** — bundles, per demand; C3 reuses the most existing substrate.
6. **F7 → F3-F6** (rename wave; analyzer first), **E0 → E1-E5** (entity moves), **G1-G5** (polish),
   **H1-H7** (hygiene) — all `[FUTURE]`, low priority, dispatch into slack windows.

---

## Sources cited

1. `shipyard/icm/01_discovery/research/erpnext-conversion-and-backlog-register-2026-05-29.md`
   (on branch `onr-erpnext-conversion-backlog-register` / shipyard#180) [PRIMARY/ONR] (2026-05-29)
2. `shipyard/_shared/product/backlog.md` — Admiral, w/ CIC 2026-05-29 rulings [PRIMARY] (2026-05-29)
3. `shipyard/_shared/engineering/erpnext-to-anchor-migration-importer-spec.md` — XO 2026-05-16
   [PRIMARY; package-name table reconciled to shipped reality in this WBS] (2026-05-29)
4. `shipyard/_shared/design/cohort-4/02-erpnext-deletion-strategy.md` — PAO 2026-05-25 [PRIMARY]
5. `shipyard/packages/foundation-catalog/Manifests/Bundles/{asset-management,project-management,facility-operations,acquisition-underwriting,property-management}.bundle.json` [PRIMARY] (2026-05-29)
6. `shipyard/packages/blocks-*` on-disk shipped-substrate survey — incl. the 3 shipped importer
   slices (`blocks-financial-ledger/Migration/ErpnextAccountImporter` + `ErpnextJournalEntryImporter`;
   `blocks-financial-ar/Migration/ErpnextSalesInvoiceImporter`) + `ImportOutcome`/`ImportAction`
   primitives [PRIMARY/merged-reality] (2026-05-29)
7. `sunfish/apps/web/src/api/erpnext.ts` consumer grep (~9 consumers incl. legacy pages + 4 tests)
   + `signal-bridge/Sunfish.Bridge/Proxy/ERPNext*.cs` family [PRIMARY/merged-reality] (2026-05-29)
8. `shipyard/_shared/engineering/bridge-data-audit.md` — M0-M5 move plan [PRIMARY] (2026-05-29)
9. `shipyard/docs/adrs/0098-block-naming-generalization.md` Rev 3 — Steps 1-2 landed, 3-7 deferred;
   TypeForwardedTo rename defect [PRIMARY] (2026-05-29)

— ONR, 2026-05-29
