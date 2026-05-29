# blocks-migration-erpnext

A0 ERPNext extraction-adapter (read side) — the sole v1 implementation of the
`IErpnextSourceExtractor` C6 access-mode seam (ADR 0100).

## What this package does

Restores a mysqldump of an ERPNext site database into an ephemeral throwaway
MariaDB/MySQL schema, then issues parameterized read-only `SELECT` statements to
stream the frozen `Erpnext*Source` DTOs into the Workstream-A import pipeline
(A1–A6).

## Security posture

- **Read-only against source.** The `IErpnextSourceExtractor` interface exposes ONLY
  `Read*Async` methods. No write/update/delete against ERPNext data.
- **SELECT-only after restore.** `MariaDbDumpExtractor` issues ONLY parameterized
  `SELECT` statements. The DDL/DML for the restore is owned by `IRestoredDbConnectionFactory`.
- **No HTTP/REST client.** No type in `Extraction/` opens a network socket to a
  Frappe/ERPNext endpoint. v1 is dump-only (offline, deterministic).
- **Throwaway DB.** The restored schema is created per run and DROPPED on completion.
  Financial data does not persist after the import.
- **No PII in logs.** Logging emits only DocType + opaque `externalRef` (ERPNext `name`)
  + counts. Party names, emails, phones, monetary amounts, and credentials are never logged.
- **Dump path from CLI/env only.** The dump file path is supplied via `--source-dump`
  CLI flag or `ERPNEXT_DUMP_PATH` env var. Never hard-coded; never echoed in logs.

## ERPNext version

Pinned to **v15**. See `ErpnextDocTypeMap.ErpnextVersion`.

## Scope (this PR)

Simple single-table extraction methods are implemented:
`ReadAccountsAsync`, `ReadCostCentersAsync`, `ReadFiscalYearsAsync`,
`ReadCustomersAsync`, `ReadSuppliersAsync`.

JOIN-requiring methods are stubs that throw `NotImplementedException` (stacked
follow-up PR): `ReadContactsAsync`, `ReadAddressesAsync`, `ReadTaxTemplatesAsync`,
`ReadJournalEntriesAsync`, `ReadSalesInvoicesAsync`, `ReadPurchaseInvoicesAsync`.

## License

MIT. See `LICENSE-ATTRIBUTION.md` for ERPNext/Frappe attribution (format-reference-only;
no Frappe code derived).
