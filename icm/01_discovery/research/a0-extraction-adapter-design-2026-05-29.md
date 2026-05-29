# ONR research — A0 ERPNext extraction-adapter design (2026-05-29)

**Requester:** Admiral (dispatched ONR subagent; CIC is the ultimate consumer — these are CIC's real books)
**Scope (in):** The DESIGN of the A0 extraction adapter — the read side of the ERPNext importer. The
adapter contract + C6 access-mode seam; the `mysqldump` → `Erpnext*Source` DTO parsing strategy and
table mapping; the clean-room / read-only / no-PII-logging enforcement; the C5 DocType-mapping authority;
and the open questions A0 surfaces to CIC.
**Scope (out):** Production code (this is a research/design deliverable, NOT the A0 build PR). The
per-record upserters (already shipped, governed by ADR 0100). The pass orchestrators A1–A7 (consume the
DTOs A0 produces; this design guarantees they need zero changes). REST/CSV adapters (DEFERRED per C6; the
seam is designed for them but v1 ships dump-only). The A7 CLI driver and Pass-6 verify (A6) — A0 only
produces the source DTOs they read.
**Status:** final (design) — feeds the A0 build PR, which is dual-council MANDATORY (sec-eng + .NET-arch)
per ADR 0100 §"Council review" / H8.

**Authoritative inputs (primary):**
- ADR 0100 — ERPNext Data Import Contract, Rev 2 (`docs/adrs/0100-erpnext-data-import-contract.md`).
  Especially C4 (clean-room), C5 (mapping authority), C6 (access-mode seam, DUMP-ONLY v1), C7
  (`ExternalRef` field), C9 (no-PII/secret logging), and the C-MODE invariant.
- Post-MVP WBS A0 row + HARD-prerequisites block (`icm/05_implementation-plan/post-mvp-wbs-2026-05-29.md`).
- The seven shipped `Erpnext*Source` DTOs on `shipyard/main` (field-level shapes read at authoring;
  paths in §2.3). These are the FROZEN target the parser must populate exactly.
- ERPNext / Frappe public data model: the `tab<DocType>` table-naming convention + standard DocType
  field labels (Frappe framework public docs; "format-reference-only, no code derived" per C4 / spec §9.5).

---

## TL;DR

1. **A0 is the C6 seam's sole v1 implementation: a `MariaDbDumpExtractor` behind an
   `IErpnextSourceExtractor` interface.** The interface exposes ONLY read operations returning the frozen
   `Erpnext*Source` DTOs; a future REST/CSV adapter implements the same interface with zero changes to
   A1–A6 (the C-MODE invariant). Owning package: a NEW small host package `blocks-migration-erpnext`
   (cross-cluster — it must reference all seven `blocks-*/Migration` DTO namespaces, which a single
   `blocks-financial-ledger/Migration/Extraction/` folder cannot do without inverting the DAG).
2. **Parsing strategy: RESTORE-TO-THROWAWAY-DB-then-query, not stream-parse the SQL text.** ERPNext stores
   child tables (JE lines, invoice items, tax rate rows) in SEPARATE `tab*` tables joined by `parent`; a
   header DTO like `ErpnextJournalEntrySource` is a JOIN across `tabJournal Entry` + `tabJournal Entry
   Account`. SQL-text stream parsing makes those joins a hand-rolled correlation nightmare and is brittle
   against mysqldump dialect quirks; restoring the dump into an ephemeral local MariaDB/MySQL (or
   embedded equivalent) and issuing parameterized read-only `SELECT`s is deterministic, gives us real
   JOINs/ORDER BY, and is exactly the surface the C4 `SELECT`-only invariant is written against.
3. **Clean-room is PROVABLE because v1 has one mode and it is the provable one.** The extractor opens the
   restored DB with a read-only connection and issues only `SELECT` (C4 (c)); no `*/Migration/`
   extraction type opens an HTTP/REST client (C4 (d) / C-MODE arch-test). The dump file lives at a
   CIC-supplied path OUTSIDE the repo tree (it is real financial data + PII), gitignored defensively;
   the DB credential comes from a CLI flag / env var and is never echoed (C9).
4. **DocType mapping is explicit, versioned, fail-loud (C5).** A0 ships a single
   `ErpnextDocTypeMap` table mapping `tab<DocType>` → extractor method → target DTO, pinned in code and
   versioned with the dump's ERPNext app version. A DocType present in the dump but absent from the map is
   COUNTED in the report's `_unmapped/` section (visible, not silently dropped); an unknown enum value
   (e.g. `account_type`) is a fail-loud HALT or bounded-fallback-with-warning per C5 — A0 surfaces these
   to the C5 mapping layer, it does not invent guesses.
5. **A0's design half has NO dependencies and ships now; the build half is gated on CIC prerequisite #1**
   (dump availability — H-CIC-1). The CIC open questions are scoped to a small confirmation set below
   (dump format/version, custom Property/Lease DocTypes, multi-currency, the LLC-chart inventory) — these
   are the inputs that turn the design into an executable build.

---

## Key findings

### 1. Adapter contract + the C6 access-mode seam

**1.1 The seam interface.** A0 introduces the single seam type the C6 invariant is written against. One
extractor method per source DTO family, each returning the frozen DTO type, each a pure read:

```csharp
namespace Sunfish.Blocks.Migration.Erpnext.Extraction;

/// Read-only ERPNext source extractor. The C6 access-mode seam: v1 ships exactly ONE
/// implementation (MariaDbDumpExtractor). A future REST/CSV adapter implements THIS interface
/// with zero changes to A1-A6. Exposes ONLY reads (C4 (a)); no write/update/delete against source.
public interface IErpnextSourceExtractor
{
    // Pass-1 chart
    IAsyncEnumerable<ErpnextAccountSource>          ReadAccountsAsync(CancellationToken ct);
    IAsyncEnumerable<ErpnextCostCenterSource>       ReadCostCentersAsync(CancellationToken ct);
    // Pass-2 reference data
    IAsyncEnumerable<ErpnextFiscalYearSource>       ReadFiscalYearsAsync(CancellationToken ct);
    IAsyncEnumerable<ErpnextPartyCustomerSource>    ReadCustomersAsync(CancellationToken ct);
    IAsyncEnumerable<ErpnextPartySupplierSource>    ReadSuppliersAsync(CancellationToken ct);
    IAsyncEnumerable<ErpnextContactSource>          ReadContactsAsync(CancellationToken ct);
    IAsyncEnumerable<ErpnextAddressSource>          ReadAddressesAsync(CancellationToken ct);
    IAsyncEnumerable<ErpnextTaxTemplateSource>      ReadTaxTemplatesAsync(CancellationToken ct);
    // Pass-3/4 transactional
    IAsyncEnumerable<ErpnextJournalEntrySource>     ReadJournalEntriesAsync(CancellationToken ct);
    IAsyncEnumerable<ErpnextSalesInvoiceSource>     ReadSalesInvoicesAsync(CancellationToken ct);
    IAsyncEnumerable<ErpnextPurchaseInvoiceSource>  ReadPurchaseInvoicesAsync(CancellationToken ct);
    // Pass-4.3 payment (DTO net-new in A4.3; the extractor method lands when the DTO does)
    // IAsyncEnumerable<ErpnextPaymentEntrySource>  ReadPaymentsAsync(CancellationToken ct);

    /// Census of DocTypes present in the source vs. the v1 mapping (C5 / report _unmapped section).
    Task<ErpnextSourceInventory> ReadInventoryAsync(CancellationToken ct);
}
```

Design notes:
- **`IAsyncEnumerable<T>` (streaming), not `Task<IReadOnlyList<T>>`.** CIC's portfolio is ~10K records
  across DocTypes (WBS / spec §10.10); streaming keeps the C5 <5-min budget honest and avoids materializing
  every JE line in memory at once. The orchestrator consumes per-record and threads each into the matching
  upserter, which already returns the C2 `ImportOutcome<T>` discriminated union.
- **The DTOs carry NO mode-specific field** (C-MODE) — they are plain records (verified: every shipped
  `Erpnext*Source` is a `sealed record` of primitives + child lists; the `ErpnextAccountSource` xmldoc's
  "as exported via REST" is illustrative, not binding — D6). The seam is the ONLY place the access mode is
  visible; above the seam everything is mode-agnostic.
- **Forward-hook for run provenance (.NET-arch (A0-seam) / C6).** `ReadInventoryAsync` returns an
  `ErpnextSourceInventory` that can carry a `SourceMode` descriptor (`"mariadb-dump"` in v1). When REST/CSV
  are added later, the orchestrator records WHICH mode produced a run in `migration-report.md` (C6
  forward-hook) — not a v1 requirement, but the field is shaped now so adding it later is non-breaking.

**1.2 Owning package — NEW `blocks-migration-erpnext`, NOT a folder under `blocks-financial-ledger`.**
The WBS A0 row offers two options ("`blocks-financial-ledger/Migration/Extraction/` OR a small NEW
`blocks-migration-erpnext` host package — A0 design decides"). **Decision: the NEW cross-cluster package.**
Rationale (a DAG argument, the same shape as ADR 0100 (G) for the outcome types):
- `IErpnextSourceExtractor` returns DTOs from SEVEN cluster namespaces — `FinancialLedger.Migration`,
  `FinancialAr.Migration`, `FinancialAp.Migration`, `People.Foundation.Migration`,
  `FinancialPeriods.Migration`, `FinancialTax.Migration`, and (future) payments. A single
  interface that mentions all seven cannot live INSIDE `blocks-financial-ledger` without that package
  taking a reference on `ar`/`ap`/`people-foundation`/etc. — which inverts the DAG (ledger is a peer of
  ar/ap, not their parent; ADR 0100 (G) names exactly this constraint for the outcome types).
- The clean placement: `blocks-migration-erpnext` sits ABOVE the cluster packages in the DAG (it
  references each cluster's `Migration` DTO namespace UP the graph; no cluster depends on it). This mirrors
  where the A7 CLI host will sit. The extraction adapter + the orchestrator (A7) are the two cross-cluster
  consumers; both belong in this host-tier package, not in any single domain cluster.
- This also keeps the sec-eng-reviewed credential/clean-room surface in ONE package (the A0 dual-council
  review has a single, bounded target), rather than scattered across `blocks-financial-ledger/`.

**1.3 What A1–A6 see.** They depend ONLY on `IErpnextSourceExtractor` + the `Erpnext*Source` DTOs. No
A-pass references `MariaDbDumpExtractor`, a connection string, or `tab*` anything. This is the literal
C-MODE acceptance test: "upserters + orchestrator depend ONLY on the `Erpnext*Source` DTOs ... a future
REST/CSV adapter requires zero changes to A1–A6."

---

### 2. mysqldump parsing approach

**2.1 RECOMMENDATION: restore-to-throwaway-DB-then-query. (Rejected: stream-parse the SQL text.)**

ERPNext/Frappe is a relational schema where a logical document is split across a parent table and one or
more child tables, correlated by the child's `parent` column. The frozen DTOs are NOT 1:1 with a single
table — three of them are header+lines JOINs:

| DTO | Parent table | Child table(s) joined |
|---|---|---|
| `ErpnextJournalEntrySource` (+ `…LineSource`) | `tabJournal Entry` | `tabJournal Entry Account` (the GL lines) |
| `ErpnextSalesInvoiceSource` (+ `…Item`) | `tabSales Invoice` | `tabSales Invoice Item` |
| `ErpnextPurchaseInvoiceSource` (+ `…Item`) | `tabPurchase Invoice` | `tabPurchase Invoice Item` |
| `ErpnextTaxTemplateSource` (+ `…RateRow`) | `tabSales Taxes and Charges Template` (and/or Purchase) | `tabSales Taxes and Charges` |
| `ErpnextContactSource` / `ErpnextAddressSource` (+ `ErpnextDynamicLink`) | `tabContact` / `tabAddress` | `tabDynamic Link` |

Reconstructing those joins by stream-parsing the `INSERT INTO ... VALUES (...)` statements out of a
mysqldump means: (a) hand-correlating child rows to parents by buffering both tables (the dump emits each
table's INSERTs as one block — you'd buffer all child rows for every parent, defeating the streaming
benefit); (b) writing a SQL-value tokenizer robust against mysqldump's escaping (`\'`, `\\`, `0x...` blobs,
`NULL` vs `'NULL'`, extended-insert vs one-row-per-statement, charset directives, the `/*!40101 ... */`
conditional-comment dialect markers); (c) re-deriving column order from the `CREATE TABLE` DDL that
precedes each block. Every one of those is a defect-surface against CIC's real financial data, and the
brittleness is exactly the kind C5's fail-loud and C8's reconcile gate would only catch AFTER a bad import.

The restore path eliminates all of it:

```
CIC produces dump.sql (one mysqldump command — §5)
        │
        ▼
A0 restores into an EPHEMERAL local DB instance (throwaway; created fresh per run, dropped after)
        │
        ▼  read-only connection, parameterized SELECT + JOIN + ORDER BY
IErpnextSourceExtractor → Erpnext*Source DTOs (streamed)
```

Tradeoffs, stated honestly:

| | Restore-then-query (RECOMMENDED) | Stream-parse SQL text |
|---|---|---|
| Child-table joins | Native SQL JOIN — correct, simple, ordered | Hand-rolled buffer+correlate; O(n) memory per parent block |
| mysqldump dialect robustness | The DB engine parses its own dump format — zero dialect risk | Must re-implement a tolerant SQL-value lexer |
| Determinism / re-runnability (C1) | High — same dump → same restored DB → same SELECTs | High, but parser bugs are silent until C8 |
| C4 `SELECT`-only provability | Direct — the connection is read-only, only `SELECT` issued (C4 (c)) | The "no DDL/DML against source" framing doesn't even apply cleanly (there's no DB) |
| Dependency footprint | Needs a MariaDB/MySQL engine available to the import host (or an embedded/container equivalent) | Pure-managed; no engine dependency |
| Offline | Yes (local restore; no network) | Yes |
| Setup cost | One-time: the host needs a mysql/mariadb client+server (or a container) | None |

The one real cost of the restore path is the engine dependency. Three concrete options for the A0 build to
pick from (a build-time decision, flagged here, not a design blocker):
- **(i) System MariaDB/MySQL on the import host** — CIC's import runs on a known machine; `mysql < dump.sql`
  into a throwaway schema named per-run (e.g. `erpnext_import_<runid>`), dropped on completion. Simplest;
  matches the spec's "import host" assumption.
- **(ii) Ephemeral container** (`docker run --rm mariadb`, load dump, query, teardown) — fully isolated,
  no host-state residue; best clean-room hygiene (the financial data lives only in a container that is
  destroyed). Adds a Docker dependency on the import host.
- **(iii) Embedded/file engine** — investigate whether a managed embedded MySQL-compatible engine can load
  a mysqldump without a server process. Lower confidence this round-trips ERPNext's DDL cleanly; flag as a
  spike, not a baseline.

A0 build SHOULD baseline (i) or (ii) and treat (iii) as an optimization. Either way the seam is identical
— `MariaDbDumpExtractor` takes a connection to the restored DB; whether that DB is system, container, or
embedded is an implementation detail BELOW the seam, invisible to A1–A6.

**2.2 The `tab<DocType>` table → DTO mapping.** ERPNext's table-naming convention is `tab` + the DocType
label (with spaces, e.g. `tabJournal Entry`). The v1 mapping the extractor pins (derived from public Frappe
docs + the observed dump shape per C4 — format-reference-only):

| `Erpnext*Source` DTO | Primary `tab*` table | Child `tab*` table | Maps to pass |
|---|---|---|---|
| `ErpnextAccountSource` | `tabAccount` | — | A1 chart |
| `ErpnextCostCenterSource` | `tabCost Center` | — | A1 cost-centers |
| `ErpnextFiscalYearSource` | `tabFiscal Year` (+ `tabFiscal Year Company` for `CompanyShortName`) | `tabFiscal Year Company` | A2.2 periods (periods are SYNTHESIZED from fiscal-year bounds — ERPNext has no per-month period DocType; the A2.2 upserter generates the period grid) |
| `ErpnextPartyCustomerSource` | `tabCustomer` | — | A2.1 parties |
| `ErpnextPartySupplierSource` | `tabSupplier` | — | A2.1 parties |
| `ErpnextContactSource` | `tabContact` | `tabDynamic Link` (party links) | A2.1 parties |
| `ErpnextAddressSource` | `tabAddress` | `tabDynamic Link` (party links) | A2.1 parties |
| `ErpnextTaxTemplateSource` | `tabSales Taxes and Charges Template` + `tabPurchase Taxes and Charges Template` | `tabSales Taxes and Charges` / `tabPurchase Taxes and Charges` (rate rows) | A2.3 tax |
| `ErpnextJournalEntrySource` | `tabJournal Entry` | `tabJournal Entry Account` (lines) | A3 opening + A4.4 manual JEs |
| `ErpnextSalesInvoiceSource` | `tabSales Invoice` | `tabSales Invoice Item` | A4.1 |
| `ErpnextPurchaseInvoiceSource` | `tabPurchase Invoice` | `tabPurchase Invoice Item` | A4.2 |
| (`ErpnextPaymentEntrySource`, A4.3 net-new) | `tabPayment Entry` | `tabPayment Entry Reference` (the allocations) | A4.3 |

Field-level mapping is direct in most cases (e.g. `tabAccount.name`→`Name`, `tabAccount.modified`→
`Modified`, `tabAccount.account_name`→`AccountName`, `tabAccount.account_number`→`AccountNumber`,
`tabAccount.parent_account`→`ParentAccountName`, `tabAccount.account_type`→`AccountType`,
`tabAccount.is_group`→`IsGroup`, `tabAccount.disabled`→`Disabled`). The non-obvious ones the A0 build must
get right:
- **`docstatus` filtering.** Submittable DocTypes (JE, both invoices, payments) carry `docstatus`
  (0=draft, 1=submitted, 2=cancelled). ERPNext's `tabJournal Entry.docstatus` maps to the DTO's `DocStatus`
  int; the orchestrator/upserter decides what to import. C8's per-DocType count reconciliation keys off
  `docstatus==1` — the extractor reads ALL rows and surfaces `docstatus`; it does NOT pre-filter (filtering
  is a pass-level policy, not an extraction concern — keeps the extractor a faithful mirror).
- **`is_opening`** on `tabJournal Entry` → `IsOpening` drives the A3 opening-balance filter.
- **`tabAddress` columns** — ERPNext uses `address_line1`/`address_line2`/`city`/`state`/`pincode`/
  `country` (the DTO field names already match).
- **`tabDynamic Link`** — the polymorphic party link table: `link_doctype`/`link_name` →
  `ErpnextDynamicLink(LinkDocType, LinkName)`, filtered to `parenttype IN ('Contact','Address')`.
- **Decimal columns** — ERPNext stores monetary values as `decimal(...)`; map straight to .NET `decimal`
  (the DTOs already use `decimal` — no float coercion, which is the CSV-mode fragility C8 would catch).
- **Date columns** — `tabFiscal Year.year_start_date` etc. are `date` → `DateOnly`; `modified` is
  `datetime` but the DTO stores it as the opaque `Modified` STRING (C1 — lexicographic ISO-8601 ordering),
  so the extractor reads it as the canonical string form, it does not parse-and-reformat.

**2.3 The frozen DTOs the parser must populate (paths verified on `main` at authoring).** These are the
contract; the extractor populates them exactly, adds no fields, drops none:
- `packages/blocks-financial-ledger/Migration/ErpnextAccountSource.cs`
- `packages/blocks-financial-ledger/Migration/ErpnextCostCenterSource.cs`
- `packages/blocks-financial-ledger/Migration/ErpnextJournalEntrySource.cs` (+ `…LineSource`)
- `packages/blocks-financial-ar/Migration/ErpnextSalesInvoiceSource.cs` (+ `…Item`)
- `packages/blocks-financial-ap/Migration/ErpnextPurchaseInvoiceSource.cs` (+ `…Item`)
- `packages/blocks-people-foundation/Migration/Pass2PartySources.cs`
  (`ErpnextPartyCustomerSource`, `ErpnextPartySupplierSource`, `ErpnextContactSource`,
  `ErpnextAddressSource`, `ErpnextDynamicLink`)
- `packages/blocks-financial-periods/Migration/ErpnextFiscalYearSource.cs`
- `packages/blocks-financial-tax/Migration/Pass2TaxSources.cs`
  (`ErpnextTaxTemplateSource`, `ErpnextTaxTemplateRateRow`)
- (`ErpnextPaymentEntrySource` — does NOT yet exist on `main`; A4.3 introduces it; the
  `ReadPaymentsAsync` extractor method lands in the same PR the DTO does.)

---

### 3. Clean-room guarantees (C4 / C9)

**3.1 Read-only against source — provable, single mode.** The extractor exposes ONLY read methods
(`Read*Async` — no `Write*`/`Update*`/`Delete*`; C4 (a) acceptance test). Against the restored throwaway
DB, the connection is opened read-only and the extractor issues only `SELECT` (C4 (c)); no DDL/DML touches
the source data. There is NO live-Frappe connection surface in v1 — the C4 (d) / C-MODE arch-test asserts
no `*/Migration/` extraction type opens an HTTP/REST client or a network socket to a Frappe/ERPNext
endpoint. Because v1 ships exactly ONE adapter and it is the dump adapter, read-only is UNIFORMLY provable
(this is the precise sec-eng (A) won't-waive rationale that collapsed C6 to dump-only — there is no
unprovable REST path to assert about).

**3.2 Where the dump file lives — OUTSIDE the repo, gitignored defensively.** The dump is CIC's real
financial books + party PII (names, emails, phones). It MUST NOT enter the repo tree:
- The dump path is a CIC-supplied CLI flag / env var (e.g. `--source-dump /secure/local/path/erpnext.sql`),
  pointing at a path OUTSIDE the fleet tree (per the fleet worktree/clean-room discipline — real financial
  data + PII never lives in a tracked location).
- Defensive `.gitignore` entries in `blocks-migration-erpnext` for `*.sql`, `*-dump.sql`, `import-source/`,
  and the throwaway-restore working directory — belt-and-suspenders so an accidental in-tree copy can't be
  committed. (The primary control is the out-of-tree path; the gitignore is the backstop.)
- The throwaway restored DB (system schema or container) is created per-run and DROPPED/torn down on
  completion — the financial data does not persist in a queryable form after the import. Container option
  (2.1 (ii)) is the strongest hygiene here (the data lives only inside an `--rm` container).

**3.3 No PII / secret logging (C9).** The extractor's logging emits ONLY: DocType, `externalRef` (the
ERPNext `name` — an opaque id, NOT PII), counts, pass/run id. It NEVER emits: the DB-restore credential
(consumed from flag/env, never echoed — C9), party names/emails/phones from `tabCustomer`/`tabContact`/
`tabAddress`, or per-line monetary amounts. Critically, the extractor is the FIRST place a record's full
contents enter the process, so the C9 discipline starts here:
- A record that fails to map at extraction time (e.g. a JE line referencing a column the DTO can't fill)
  is surfaced as a structured reject — `{reasonCode, docType, externalRef, fieldName?}` — NEVER the raw
  row payload (the C9 / sec-eng (B) allowlisted-projection invariant; a serializer that dumps the raw
  `Erpnext*Source` into a log or `reject_detail` is forbidden).
- The C-LOG / C-LOG-REJECT acceptance tests (ADR 0100 C9) land at the A0 PR (extraction log capture) and
  A2.1 PR (the party-PII reject path). sec-eng owns both at council review.

**3.4 License / attribution header (C4 (b)).** The `MariaDbDumpExtractor` source file carries the
attribution header naming ERPNext + Frappe + GPLv3 + "format-reference-only; no code derived" (spec §9.5).
A0 reads ERPNext's DATA FORMAT only (the `tab*` table+column shape, which is the public data-interchange
contract) — NOT Frappe controllers, validators, workflow code, or DocType-definition JSON (C4 / spec
§9.1–§9.2). The `tab*`/column knowledge comes from public Frappe docs and the observed dump, both
data-format references.

---

### 4. DocType mapping authority (C5)

**4.1 The mapping is explicit, versioned, and in code.** A0 ships a single `ErpnextDocTypeMap` — the one
authoritative table mapping each supported `tab<DocType>` to its extractor method and target DTO. It is
pinned in code (the spec §3 tables are the canonical v1 mapping per C5), versioned with the ERPNext app
version the dump came from (so a future ERPNext-schema change is a deliberate map revision, not a silent
drift). This is the SHAPE map (table→DTO); the SEMANTIC maps (e.g. `account_type`→`GLAccountType`,
`voucher_type`→entry kind) live in the per-pass upserters/orchestrators (A1+), which already own them — A0
does not duplicate them.

**4.2 Unknown DocTypes — counted, visible, never silently dropped.** `ReadInventoryAsync` enumerates every
`tab*` table present in the restored dump and partitions them:
- **Mapped** — in `ErpnextDocTypeMap`; extracted normally.
- **Known-irrelevant** — an allowlist of ERPNext system/framework DocTypes the importer deliberately
  ignores (`tabDocType`, `tabSingles`, `tabSeries`, `tabVersion`, session/permission tables, etc.). These
  are NOT financial data; the allowlist keeps the report's unmapped section signal-rich.
- **Unmapped-unknown** — present in the dump, financial-or-business-looking, NOT in the map and NOT on the
  ignore allowlist. These are COUNTED and listed in the migration report's `_unmapped/` section with a
  non-zero count (spec §2.2 / C5 acceptance test C-MAP) — VISIBLE to CIC, never an error, never silently
  dropped. This is exactly how a custom `Property`/`Lease` DocType (H-CIC-2) would surface if present:
  `tabProperty` shows up as unmapped-unknown, and CIC's prereq #2(i) answer tells A5b whether to build
  upserters for it.

**4.3 Unknown enum VALUES are a pass-level C5 concern, surfaced by A0.** A0 extracts `account_type`,
`voucher_type`, etc. as the raw source strings into the DTOs faithfully. The fail-loud handling — an
unknown `account_type` HALTs Pass 1; an unknown `voucher_type` imports as `Manual` with a Pass-6 warning
(C5) — happens in the A1/A4 upserters that own the semantic mapping, NOT in A0. A0's responsibility is to
deliver the raw value un-coerced and un-guessed so the C5 fail-loud layer has the true source string to
reason about. (A0 does NOT, e.g., default a NULL `account_type` to a guess — it passes the NULL through;
the upserter's parent-walk + C5 HALT decides.)

---

### 5. Open questions for CIC

These are the inputs that turn the A0 design into an executable build. Items 1–4 mirror ADR 0100's
H-CIC-1..4 and the WBS HARD-prerequisites block; A0 sharpens them to the extraction specifics.

1. **(H-CIC-1, blocks A0 build) Dump availability + format/version.** Confirm CIC can produce a static
   MariaDB dump of the ERPNext site DB (the sole v1 mode). A0 needs: (a) the ERPNext/Frappe app VERSION
   (the `tab*` schema + `account_type`/`voucher_type` enums are version-sensitive — pins `ErpnextDocTypeMap`
   version); (b) the MariaDB/MySQL server version + charset/collation (affects the restore engine choice in
   §2.1); (c) confirmation the dump can be a FULL site-DB dump (all `tab*` tables) vs a curated subset.
   A0 will produce the exact `mysqldump` command + the `tab*` table allowlist as the CIC-input ask once
   version is known (e.g. `mysqldump --single-transaction --no-create-db --skip-extended-insert <site_db> > erpnext.sql`,
   table list scoped to the §2.2 mapping + child tables).
2. **(H-CIC-2, blocks A5b scope) Custom Property/Lease DocTypes + chart inventory + multi-currency.**
   (i) Does the instance have CUSTOM `tabProperty` / `tabLease` / `tabUnit` DocTypes, or is `Cost Center`
   abused as the property dimension? (A0's `ReadInventoryAsync` will reveal this empirically once a dump
   exists, but a pre-answer lets A5b be scoped now.) (ii) Which companies/LLCs are charts — the spec assumes
   4 (Acero/Bosco/Escola/Shirin); the extractor reads `tabCompany` to confirm. (iii) Multi-currency: ADR
   0100 OQ-2 resolved v1 to USD-only (reject + log non-base-currency records); CONFIRM CIC's 4 LLCs are all
   USD so the guardrail is a guardrail, not a common path that silently rejects real records.
3. **(H-CIC-3, blocks A1+ write) Target Sunfish tenant id.** The `--target-tenant <id>` the import lands
   into (single tenant; C3 fail-closed sentinel validation). Not strictly an A0-extraction input (A0 is
   read-only against source), but the run can't complete without it — flag it here for the packaged ask.
4. **(H-CIC-4, strongly recommended for a real-books cutover) Reconcile snapshots.** CIC-produced ERPNext-
   side `gl-balances-snapshot.json` / `ar-aging-snapshot.json` / `ap-aging-snapshot.json` for Pass-6
   verification (C8). Without them, Pass 6 HALTS unless `--no-reconcile-snapshots` is explicitly passed
   (the trial-balance-only path verifies arithmetic, not source fidelity). A0 does not consume these (A6
   does), but the same CIC export session that produces the dump is the cheapest time to produce them — so
   ONR recommends bundling the snapshot ask with the dump ask.

A0-specific residue (no CIC ruling needed; flagged for the A0 build to decide): the restore-engine choice
(§2.1 (i)/(ii)/(iii)) is a build-time decision constrained by the import host's environment — resolvable
once CIC confirms where the import runs (the same machine that holds the dump).

---

## Sources cited

1. **(Primary)** ADR 0100 — ERPNext Data Import Contract, Rev 2. `docs/adrs/0100-erpnext-data-import-contract.md`.
   Status: Accepted (pending dual-council RE-ATTEST GREEN). Authored 2026-05-29; retrieved 2026-05-29.
   Load-bearing clauses: C4, C5, C6, C7, C9, the C-MODE / C-CLEANROOM / C-MAP / C-LOG / C-LOG-REJECT
   acceptance tests, the §A0 cited-symbol audit, the §"Revision 2 fold" amendments (G).
2. **(Primary)** Post-MVP WBS — `icm/05_implementation-plan/post-mvp-wbs-2026-05-29.md`, Workstream A,
   A0 row + HARD-prerequisites block. ONR; retrieved 2026-05-29.
3. **(Primary)** The seven shipped `Erpnext*Source` DTOs on `shipyard/main` (field-level shapes,
   paths in §2.3). Read at HEAD `1bf813e`; retrieved 2026-05-29. These are the FROZEN extraction target.
4. **(Primary)** ERPNext / Frappe Stage-03 importer spec —
   `shipyard/_shared/engineering/erpnext-to-anchor-migration-importer-spec.md` (XO 2026-05-16). §1.2 / §9
   (clean-room), §2.2 (`_unmapped/` directory), §3 (DocType mapping tables), §10.6 (export-script
   ownership). Folded by ADR 0100; cited via the ADR.
5. **(Secondary)** Frappe framework public data model — the `tab<DocType>` table-naming convention + standard
   DocType field labels + child-table `parent`/`parenttype` correlation. Format-reference-only per C4 /
   spec §9.5 (no Frappe code derived; data-interchange contract only). Public Frappe docs; retrieved
   2026-05-29 (general knowledge; not a single URL — the `tab*` convention is stable across ERPNext v13–v15,
   but the exact column set is version-sensitive — see CIC open question #1).

---

— ONR, 2026-05-29. Feeds the A0 build PR (dual-council MANDATORY: sec-eng + .NET-architect per ADR 0100 H8).
The design half has no dependencies and is complete; the build half is gated on CIC prerequisite #1 (dump
availability — open question #1).
