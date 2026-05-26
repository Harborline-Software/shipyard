# @sunfish/contracts

Shared TypeScript interface definitions for the Sunfish ERPNext stack. Single entry point for
downstream consumers (tender, flight-deck, self-hosters, mobile clients).

## Namespaces

| Namespace   | Types exported | Source of truth |
|-------------|----------------|-----------------|
| `property`  | `Property`, `Unit`, `OccupancyStatus`, `RentStatus`, `RentRollRow` | Bridge property endpoints |
| `accounting`| `LedgerEntry`, `JournalEntry`, `BankTransaction`, `PLSummary`, `PLLineItem`, `OutstandingInvoice` | Bridge accounting endpoints |
| `tenant`    | `Tenant`, `Lease`, `PaymentRecord`, `MessageThread` | Bridge tenant endpoints |
| `sync`      | `SyncStatus`, `OfflineQueueEntry`, `ConflictRecord` | Offline-first sync layer |
| `integrations` | `IntegrationAtlasView`, `IntegrationCategory`, ... | ADR 0067 |
| `system-requirements` | `SystemRequirementsResult`, `OverallVerdict`, ... | ADR 0063 |
| `bundles`   | `BusinessCaseBundleManifest`, `BundleCategory`, `BundleStatus`, `DeploymentMode`, `ProviderCategory`, `ProviderRequirement`, `MinimumSpec` | ADR 0007 + A1 |

## Bundle-manifest namespace — drift discipline

The `bundles` namespace mirrors the C# record
`Sunfish.Foundation.Catalog.Bundles.BusinessCaseBundleManifest`
(canonical source: `packages/foundation-catalog/Bundles/BusinessCaseBundleManifest.cs`).

**Drift discipline:**

- The C# record is canonical. Any field additions, type changes, or removals to the C# record
  MUST be reflected in `src/bundles.ts` before the change ships.
- The fixture-roundtrip test at `src/__tests__/bundles.test.ts` loads each of the 5 canonical
  `*.bundle.json` files from `packages/foundation-catalog/Manifests/Bundles/` and asserts shape
  conformance against the TypeScript interfaces. This test is the primary CI-time drift detector.
- Rust consumers (tender) carry a serde struct mirror in `tender/apps/desktop/src-tauri/src/bundles.rs`.
  Rust drift is caught by that repo's `cargo test` against the same fixture files.

All three mirrors (TypeScript, Rust, C#) must remain in sync. When the C# record changes,
update TypeScript here + Rust in tender, and ensure the fixture JSON files are also updated.

## Usage

```ts
import type { BusinessCaseBundleManifest } from '@sunfish/contracts'
```

## Development

```bash
npm run build       # compile TypeScript
npm run typecheck   # type-check without emit
npm test            # run vitest suite (includes bundle fixture-roundtrip)
```
