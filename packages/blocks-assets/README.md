# Sunfish.Blocks.Assets

The asset-management block — a property-agnostic asset domain (ADR 0101 C1.1) plus a read-display file-catalog UI surface. The two are orthogonal: the domain is the substantive content; the catalog is a thin UI block.

**Naming note:** the property-operations cluster's property-scoped physical-equipment domain ships separately as [`Sunfish.Blocks.PropertyEquipment`](../blocks-property-equipment/README.md) per UPF Rule 4 (the `Equipment` rename, 2026-04-28). This block's domain (`Asset`) is the property-*agnostic* generalization — fleet, manufacturing, facility, IT-hardware assets that do not belong to a `Property`. The two domains coexist (deliberate near-term duplication; a future ADR may unify them — see ADR 0101 Consequences).

## What this ships

### Asset domain (ADR 0101 C1.1, namespace `Sunfish.Blocks.Assets.Domain` / `.Services`)

- **`Asset`** (`IMustHaveTenant`) — the property-agnostic asset entity: category, make/model/serial, acquisition cost (typed `Money` from day one, ADR 0051), warranty, depreciation schedule, free-text `Location` (NOT a `PropertyId`), lifecycle state, soft-delete via `DisposedAt`.
- **`AssetId`** — strongly-typed opaque id (`readonly record struct`, `NewId()`, JSON converter).
- **`AssetCategory`** — closed enum first-slice (fleet-vehicle, manufacturing-equipment, facility-asset, IT-hardware, …); registry backing (`kernel-schema-registry`) is a named follow-up unit, NOT part of C1.1.
- **`AssetLifecycleEvent`** (`IMustHaveTenant`) + **`AssetLifecycleEventType`** — append-only lifecycle history. Deliberately carries NO `Property` snapshot (ADR 0101 A3 — the asset is property-agnostic).
- **`LifecycleState`** — Draft / Active / InMaintenance / Retired / Disposed.
- **`DepreciationSchedule`** + **`DepreciationMethod`** — pure computation (straight-line, declining-balance, units-of-production, none). Auto-calc opt-in (defaults off).
- **`WarrantyTerm`** — coverage window + provider; basis for the warranty-expiry query.
- **`IAssetRepository`** + **`IAssetLifecycleEventStore`** — tenant-scoped on every call; reject the system / default tenant. In-memory impls + `AddInMemoryAssets()` DI extension.

### File-catalog UI (orthogonal surface)

- **`AssetRecord`** (namespace `Sunfish.Blocks.Assets.Models`) — read-display file entry.
- **`AssetCatalogBlock.razor`** — composes `SunfishDataGrid` + `SunfishFileManager` into a browse-assets UI.

## Cluster role

The asset-management domain core for the `asset-management.bundle.json` reference bundle. The Bridge endpoints (C1.2), cockpit React pages (C1.3), and bundle activation (C1.4) build on this substrate. Takes NO dependency on `blocks-property-equipment` / `blocks-properties` (D3 — property-coupling forbidden on a property-agnostic block).

## See also

- [apps/docs Overview](../../apps/docs/blocks/assets/overview.md)
- [Sunfish.Blocks.PropertyEquipment](../blocks-property-equipment/README.md) — the property-cluster physical-equipment domain (different scope; coexists)
