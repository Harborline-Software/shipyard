---
title: Q6 Tender Deep Integration — Stage 05 Spec
ratification-trace: admiral-ruling-2026-05-25T2200Z-cic-slotting-6-residual-questions-resolved §Q6
ratification-date: 2026-05-25T22:00Z
ratification-authority: CIC direct ratification 2026-05-25T21:55Z
workstream: NONE (slotting-residual ratification, not a workstream-pre-auth — treated structurally like a cohort hand-off for spec rigor; routes via direct PR cluster on tender + shipyard repos)
cluster: tender-q6-deep-integration (cross-repo: `tender/apps/desktop/` + `shipyard/packages/contracts/`)
pipeline: tender-feature-change + shipyard-contracts-extension (split-repo PR cluster)
authored-by: ONR
authored-at: 2026-05-25T2300Z
status: ready-for-halt-resolution (5 halts pre-spec'd for Admiral; PR A is unblocked AFTER halts H1+H2 resolve)
pre-auth-status: NOT pre-authorized (Q6 is CIC-ratified scope — pre-auth not needed; PRs ship under standard tier-3 capability-plugin reader patterns)
depends-on:
  - Q6 ratification (admiral-ruling-2026-05-25T2200Z §Q6) — DONE
  - po-mac Q6 pre-flight survey (admiral-directive-2026-05-25T2245Z §P1) — IN PROGRESS as of this spec
  - ADR 0007 Bundle Manifest Schema (Accepted 2026-04-19; +A1 2026-05-01) — canonical source-of-truth shape
  - tender's existing fleet-services Rust telemetry surface (`tender/apps/desktop/src-tauri/src/telemetry.rs`) — extension point for plugin-health surfacing
  - sunfish/ui-react `file:` ref precedent (canonical example of pnpm `file:` consumption from sibling fleet repo)
spec-source: |
  - `coordination/inbox/admiral-ruling-2026-05-25T2200Z-cic-slotting-6-residual-questions-resolved.md` §Q6 (PRIMARY — CIC ratification)
  - `coordination/inbox/admiral-directive-2026-05-25T2245Z-po-mac-tender-q6-and-mac-substrate.md` §P1 (po-mac pre-flight directive)
  - `shipyard/docs/adrs/0007-bundle-manifest-schema.md` (canonical shape; A1 adds Requirements field)
  - `shipyard/packages/foundation-catalog/Bundles/BusinessCaseBundleManifest.cs` (canonical C# record — exact field types)
  - `shipyard/packages/foundation-catalog/Manifests/Bundles/*.bundle.json` (5 existing manifests: property-management, asset-management, project-management, facility-operations, acquisition-underwriting)
  - `shipyard/packages/contracts/src/index.ts` + `package.json` (consumption-tier package geometry)
  - `tender/apps/desktop/src-tauri/src/telemetry.rs` + `commands.rs` + `lib.rs` (extension points)
  - `[[three-tier-slotting-vocabulary]]` — Q6 surfaces tier-3 capability-plugin health in tender (bundle manifests are tier-3 substrate per Q2 promotion path; today they live in foundation-catalog as embedded resources)
estimated-effort: ~1-day total dev across 2 PRs (split-repo; ~3-4h shipyard contracts extension + ~4-6h tender Rust+UI integration)
PR-count: 2 primary + 1 optional refinement (recommended split — see §2)
pre-merge-council:
  security-engineering: NOT REQUIRED (no new authentication / network / write-path surface; tender's local filesystem reads stay local; no new HTTP endpoints; bundle manifests are public configuration not secrets)
  dotnet-architect: NOT REQUIRED (no .NET code changes; shipyard PR extends @sunfish/contracts TypeScript package only — no foundation-catalog C# surface modification)
  frontend-reviewer: SPOT-CHECK RECOMMENDED on PR B (React component for plugin-health visualization is a net-new UI surface in tender; existing tender UI has fleet-services + system-stats panels but no slot/plugin display; minor surface but first-instance)
license-posture: MIT clean-room (all new TypeScript + Rust + React code is original; reading public bundle-manifest JSON shapes; no FOSS source studied)
---

# Stage 05 — Q6 Tender Deep Bundle-Manifest Integration

**From:** ONR (Office of Naval Research; research session)
**To:** po-mac (primary executor per CIC ratification §Q6 owner); Engineer-corps fallback if po-mac saturated; Admiral routes both
**Ratification:** CIC direct ratification 2026-05-25T21:55Z (admiral-ruling-2026-05-25T2200Z §Q6)
**Pipeline:** split-repo `tender-feature-change` + `shipyard-contracts-extension`

---

## 1. Scope + ratification trace

### 1.1 What Q6 ratified

Per CIC ratification 2026-05-25T22:00Z (admiral-ruling §Q6), Tender adopts **deep bundle-manifest integration**:

1. **`@sunfish/contracts` `file:` ref** wired into Tender's package.json (precedent: sunfish/ui-react `file:` pattern; pnpm sibling-repo workspace shape).
2. **Bundle-manifest reader in Tender Rust core** — serde struct mirror of `BusinessCaseBundleManifest`; reads from on-disk JSON manifests via stable filesystem path.
3. **UI surface for slot satisfaction + plugin health display** — React component reachable from the Tender menubar, surfacing bundle inventory + per-bundle module / provider-requirement satisfaction.

Cost (per ratification): ~1-day po-mac lift. Rationale: cleanest long-term per CIC directive 2026-05-21; deep integration enables Tender to surface plugin health + slot satisfaction natively (no HTTP-hop indirection through flight-deck or Bridge).

### 1.2 What Q6 does NOT ratify (out of scope here)

- **Q2 plugin-substrate promotion** (flight-deck/packages/plugins/manifest-schema.json → shipyard/packages/foundation-plugins/) — separate Stage-05 hand-off, separate Engineer lift, post-MVP. Q6 here consumes bundle-manifests; the flight-deck plugin-manifest substrate is a sibling Q2 effort.
- **flight-deck HTTP retirement** — tender currently calls flight-deck via HTTP for emergency-stop + health probes (see `tender/apps/desktop/src-tauri/src/commands.rs::emergency_stop` and `telemetry.rs::detect_flight_deck`). Q6 does NOT retire those paths; it adds deep bundle-manifest reads alongside. flight-deck HTTP path retirement is a separate question (see Halt H5).
- **Provider-instance probing** — when a bundle declares `providerRequirements: [{category: "Payments", required: true}]`, Q6 surfaces the REQUIREMENT in the UI but does NOT probe whether a payments provider is actually configured + healthy. Provider-instance health is downstream (depends on Q2 promotion + a Tier-2 provider registry; not in Q6's lift).
- **Bundle activation** — tender is a read-only surface (display bundle inventory + slot satisfaction). Activating a bundle for a tenant remains Bridge's job (per ADR 0006 + ADR 0007 §Decision).
- **MinimumSpec evaluation** — bundles MAY carry `requirements: MinimumSpec?` per ADR 0007-A1; tender surfaces the field if present but does NOT evaluate it against the host's MissionEnvelope (that's ADR 0063 Phase 2 wiring, out of scope).

### 1.3 Three-tier vocabulary placement

Per Q1 ratification (admiral-ruling §Q1 same ruling), the fleet's three-tier vocabulary is:

- **Tier 1 (`domain-block`)** — concrete DI; never swapped.
- **Tier 2 (`category-provider`)** — bounded vendor swap.
- **Tier 3 (`capability-plugin`)** — runtime swap via manifest.

Q6's bundle manifests are NOT plain tier-3 capability-plugins. They are **configuration artifacts that COMPOSE tier-1 blocks + declare tier-2 provider requirements**. The bundle manifest is itself a substrate-tier composition descriptor — it sits ABOVE the three-tier slot taxonomy. Tender's UI surfaces:

- **Bundle inventory** (5 bundles today; can grow): the configuration set.
- **Per-bundle module satisfaction** (tier-1 block presence — are all `requiredModules` installable?): mostly informational since tier-1 is never swapped, but useful for "is this bundle's tier-1 surface complete?" diagnostic.
- **Per-bundle provider-requirement satisfaction** (tier-2 category-provider presence — is each `providerRequirements` category satisfied?): the meaningful slot/health story.
- **Per-bundle MinimumSpec acceptance** (host meets `requirements`? — informational, not gating in Q6).

The "plugin health" framing from the Q6 ratification text is a useful but slightly imprecise label — what tender actually surfaces is **bundle-manifest health: which configurations would activate cleanly on this host**, with per-bundle drill-down to tier-2 provider-category satisfaction.

### 1.4 ratification trace summary

| Ratification | Date | Authority | Effect on this Stage-05 |
|---|---|---|---|
| CIC §Q6 deep bundle-manifest integration | 2026-05-25T22:00Z | CIC direct | Authorizes the 3-component decision |
| CIC §Q1 three-tier vocabulary | 2026-05-25T22:00Z | CIC direct | Bundle manifests sit ABOVE tier-3; tender surfaces tier-2 satisfaction per-bundle |
| CIC directive 2026-05-21 (cleanest long-term) | 2026-05-21 | CIC direct | Justifies `file:` deep over HTTP-hop loose |
| ADR 0007 Bundle Manifest Schema | 2026-04-19 | Accepted | Canonical shape — no schema modification in Q6 |
| ADR 0007-A1 (Requirements field) | 2026-05-01 | Accepted | Manifest mirror MUST include optional `requirements: MinimumSpec?` field |

---

## 2. Per-PR breakdown

Q6 splits cleanly into 2 PRs across 2 repos (shipyard + tender). The split is recommended over a single mega-PR because:

- Shipyard PR has a NARROW surface (TypeScript type addition + export) → fast review, low risk, lands first as precondition.
- Tender PR has the LARGER surface (Rust reader + Tauri commands + React UI) → benefits from landing on a stable shipyard PR A.
- Split-repo PRs cannot be atomic anyway (separate `gh pr merge` operations); explicit ordering is clearer than implicit.

An optional PR C is described but NOT required (refinement only — see §2.4).

### 2.1 PR A (shipyard) — `@sunfish/contracts` bundle-manifest type export

**Repo:** `shipyard`
**Worktree:** `shipyard/.worktrees/feat-contracts-bundle-manifest/`
**Branch:** `feat/contracts-bundle-manifest`
**Title:** `feat(contracts): export BusinessCaseBundleManifest TypeScript type`
**Effort:** ~3-4h
**Pattern claimed:** none (additive export; no new pattern)
**Pre-merge council:** none required
**Auto-merge eligible:** YES (no security, no UI, no write-path; pure additive type export)

#### 2.1.1 Scope

Add a TypeScript mirror of `BusinessCaseBundleManifest` (ADR 0007 + A1 shape) to `@sunfish/contracts`, exported as a new namespace `bundles`.

**Files modified:**

| File | Modification |
|---|---|
| `shipyard/packages/contracts/src/bundles.ts` | NEW — TypeScript interface mirror |
| `shipyard/packages/contracts/src/index.ts` | Add `export * from './bundles.js'` |
| `shipyard/packages/contracts/__tests__/bundles.test.ts` | NEW — vitest fixture-roundtrip test against the 5 existing `*.bundle.json` files |
| `shipyard/packages/contracts/README.md` | (if exists) — add bundle-manifest namespace to the namespace list |
| `shipyard/packages/contracts/package.json` | (none — version bump deferred until consumer count > 1, per existing 0.1.0 convention) |

#### 2.1.2 TypeScript shape

Mirror the C# `BusinessCaseBundleManifest` (and supporting enums + records) field-for-field. ALL field names use camelCase as ADR 0007 §Decision states ("deserializable from JSON via System.Text.Json with default camelCase property names"). The 5 existing `*.bundle.json` files on disk are the canonical JSON-shape fixture.

```ts
// shipyard/packages/contracts/src/bundles.ts

/**
 * Business-case bundle manifest. Mirrors Sunfish.Foundation.Catalog.Bundles.BusinessCaseBundleManifest
 * (ADR 0007 + A1). Read-only consumption shape for non-.NET consumers (tender, etc.).
 *
 * Authoritative source: shipyard/packages/foundation-catalog/Bundles/BusinessCaseBundleManifest.cs
 * Canonical JSON fixtures: shipyard/packages/foundation-catalog/Manifests/Bundles/*.bundle.json
 */
export interface BusinessCaseBundleManifest {
  key: string;                              // required
  name: string;                              // required
  version: string;                           // required (semver)
  description?: string | null;
  category: BundleCategory;
  status: BundleStatus;
  maturity: string;                          // free-form
  requiredModules: string[];
  optionalModules: string[];
  featureDefaults: Record<string, string>;
  editionMappings: Record<string, string[]>;
  deploymentModesSupported: DeploymentMode[];
  providerRequirements: ProviderRequirement[];
  integrationProfiles: string[];
  seedWorkspaces: string[];
  personas: string[];
  dataOwnership?: string | null;
  complianceNotes?: string | null;
  // Per ADR 0007-A1; optional; absent in pre-A1 manifests.
  // System.Text.Json with JsonIgnoreCondition.WhenWritingNull → field omitted from JSON when null.
  requirements?: MinimumSpec | null;
}

export type BundleCategory = 'Operations' | 'Diligence' | 'Finance' | 'Platform';

export type BundleStatus = 'Draft' | 'Preview' | 'GA' | 'Deprecated';

export type DeploymentMode = 'Lite' | 'SelfHosted' | 'HostedSaaS';

export type ProviderCategory =
  | 'Billing'
  | 'Payments'
  | 'BankingFeed'
  | 'FeatureFlags'
  | 'ChannelManager'
  | 'Messaging'
  | 'Storage'
  | 'IdentityProvider'
  | 'Other';

export interface ProviderRequirement {
  category: ProviderCategory;
  required: boolean;
  purpose?: string | null;
}

/**
 * Per ADR 0007-A1. Foundation-catalog-local stub today; will be replaced by
 * Sunfish.Foundation.MissionSpace.MinimumSpec when ADR 0063 Phase 1 substrate
 * ships. Field signature unchanged across the future rename. Tender SHOULD
 * treat this field as opaque-display only — do NOT evaluate against host
 * MissionEnvelope (ADR 0063 Phase 2 wiring is out of Q6 scope).
 */
export interface MinimumSpec {
  // Intentionally minimal — Phase 1 stub. Tender displays this as an
  // information panel; doesn't evaluate. When ADR 0063 ships, this type
  // is replaced by the canonical Sunfish.Foundation.MissionSpace.MinimumSpec
  // shape (10 dimensions + SpecPolicy + PerPlatformSpec).
  policy?: 'Required' | 'Recommended' | 'Informational';
  // Additional fields tolerated via excess-properties for forward-compat.
  [key: string]: unknown;
}
```

#### 2.1.3 Fixture-roundtrip test (`bundles.test.ts`)

Test loads each of the 5 existing `*.bundle.json` files via `fs.readFileSync` + `JSON.parse`, casts to `BusinessCaseBundleManifest`, and verifies:

- `key` is non-empty
- `version` matches semver `^\d+\.\d+\.\d+$`
- `category` ∈ `BundleCategory` literal union
- `status` ∈ `BundleStatus` literal union
- `requiredModules.length >= 0` (sentinel: type-shape sanity)
- `requirements` is either absent OR a valid object (for forward-compat with future A1-conformant manifests; today's 5 fixtures pre-date A1 so `requirements` is absent — test asserts absence is acceptable)

Path the test against `../../foundation-catalog/Manifests/Bundles/*.bundle.json` using relative resolution from the contracts package; this catches drift between the C# canonical shape and the TS mirror at test time.

#### 2.1.4 What PR A does NOT do

- Does not export `IBundleCatalog` runtime interface (that's a .NET-only runtime concept; tender doesn't use it — tender reads JSON directly).
- Does not export `BundleManifestLoader` (same reason — JSON parsing in Rust uses serde, not the C# loader).
- Does not version-bump `@sunfish/contracts` package.json (still 0.1.0 — version bumps happen when external consumers care; today the only consumer is the internal pnpm workspace + soon tender via `file:`).
- Does not modify the C# `BusinessCaseBundleManifest` record or any of the 5 existing `*.bundle.json` source files — pure additive TypeScript export.

#### 2.1.5 Halt conditions BEFORE PR A opens

H1 + H2 (see §5) gate PR A authoring. H3-H5 are PR B halts. Once Admiral resolves H1 + H2, PR A is mechanical (~3-4h).

#### 2.1.6 Acceptance criteria

- [ ] `shipyard/packages/contracts/src/bundles.ts` exports the 6 type names above with matching shapes
- [ ] `shipyard/packages/contracts/src/index.ts` re-exports the bundles namespace
- [ ] `pnpm --filter @sunfish/contracts build` succeeds
- [ ] `pnpm --filter @sunfish/contracts test` passes including fixture-roundtrip against all 5 `*.bundle.json` files on disk
- [ ] `pnpm --filter @sunfish/contracts typecheck` clean
- [ ] No modification to `foundation-catalog/Bundles/*.cs` or `Manifests/Bundles/*.bundle.json`
- [ ] CI green
- [ ] Auto-merge fires

### 2.2 PR B (tender) — bundle-manifest reader + Tauri commands + React UI

**Repo:** `tender`
**Worktree:** `tender/.worktrees/feat-q6-bundle-manifest-reader/`
**Branch:** `feat/q6-bundle-manifest-reader`
**Title:** `feat(desktop): Q6 bundle-manifest reader + slot-satisfaction UI`
**Effort:** ~4-6h
**Pattern claimed:** none (first-instance tender consumption of shipyard contracts; no formal pattern emerges from one usage — flag as candidate if a second tender↔shipyard contracts wire appears)
**Pre-merge council:** frontend-reviewer SPOT-CHECK RECOMMENDED (first net-new UI panel in tender beyond fleet-services/system-stats/devices; minor surface)
**Auto-merge eligible:** depends on frontend SPOT-CHECK disposition; default YES if no frontend hold

#### 2.2.1 Scope

Add a bundle-manifest reader to tender's Rust core, expose it via Tauri commands, and surface bundle inventory + slot satisfaction in a new React component reachable from the tender menubar.

**Files modified (tender repo):**

| File | Modification |
|---|---|
| `tender/apps/desktop/package.json` | Add `"@sunfish/contracts": "file:../../../shipyard/packages/contracts"` to `dependencies` |
| `tender/apps/desktop/src-tauri/Cargo.toml` | (no change — `serde` + `serde_json` already declared) |
| `tender/apps/desktop/src-tauri/src/bundles.rs` | NEW — `BundleManifestReader` module: serde structs + JSON file reader + error type |
| `tender/apps/desktop/src-tauri/src/commands.rs` | Add `get_bundle_manifests()` + `get_bundle_slot_satisfaction(bundle_key)` Tauri commands |
| `tender/apps/desktop/src-tauri/src/lib.rs` | Add `mod bundles;` + register the 2 new commands in `invoke_handler!` |
| `tender/apps/desktop/src/components/BundlesPanel.tsx` | NEW — React panel displaying bundle inventory + drill-down |
| `tender/apps/desktop/src/components/BundleHealthBadge.tsx` | NEW — small status pill per bundle (counts of satisfied/unsatisfied provider-categories) |
| `tender/apps/desktop/src/App.tsx` (or wherever the menubar panel switcher lives) | Add navigation entry for the bundles panel |
| `tender/apps/desktop/src/types/bundles.ts` | NEW — small adapter file re-exporting from `@sunfish/contracts` for tender-local convenience (optional; reduces import path noise) |
| `tender/.wolf/anatomy.md` | OpenWolf update for new files |
| `tender/CHANGELOG.md` (if exists) | Q6 entry |

**Files NOT modified:**

- `tender/apps/desktop/src-tauri/src/telemetry.rs` — fleet-services surface stays as-is; bundle-manifests are a sibling concern (different panel, different Tauri commands, different Rust module).
- `tender/apps/desktop/src-tauri/src/devices.rs` — Tailscale device surface untouched.
- `tender/apps/desktop/src-tauri/src/notifications.rs` — service-state notifications untouched. (Future: notify on bundle-manifest drift? Out of Q6 scope.)

#### 2.2.2 Rust serde mirror (`bundles.rs`)

Mirror the TypeScript shape from PR A field-for-field. Use `#[serde(rename_all = "camelCase")]` to match the JSON-on-disk convention (ADR 0007 default camelCase). Use `Option<T>` for nullable C# fields; use `Vec<T>` defaulting empty for the collections.

```rust
// tender/apps/desktop/src-tauri/src/bundles.rs

use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::path::PathBuf;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct BusinessCaseBundleManifest {
    pub key: String,
    pub name: String,
    pub version: String,
    #[serde(default)]
    pub description: Option<String>,
    pub category: BundleCategory,
    pub status: BundleStatus,
    #[serde(default)]
    pub maturity: String,
    #[serde(default)]
    pub required_modules: Vec<String>,
    #[serde(default)]
    pub optional_modules: Vec<String>,
    #[serde(default)]
    pub feature_defaults: HashMap<String, String>,
    #[serde(default)]
    pub edition_mappings: HashMap<String, Vec<String>>,
    #[serde(default)]
    pub deployment_modes_supported: Vec<DeploymentMode>,
    #[serde(default)]
    pub provider_requirements: Vec<ProviderRequirement>,
    #[serde(default)]
    pub integration_profiles: Vec<String>,
    #[serde(default)]
    pub seed_workspaces: Vec<String>,
    #[serde(default)]
    pub personas: Vec<String>,
    #[serde(default)]
    pub data_ownership: Option<String>,
    #[serde(default)]
    pub compliance_notes: Option<String>,
    /// Per ADR 0007-A1. Optional; tender treats opaque-display only.
    #[serde(default)]
    pub requirements: Option<MinimumSpec>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum BundleCategory {
    Operations, Diligence, Finance, Platform,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum BundleStatus {
    Draft, Preview, GA, Deprecated,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum DeploymentMode {
    Lite, SelfHosted, HostedSaaS,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ProviderRequirement {
    pub category: ProviderCategory,
    pub required: bool,
    #[serde(default)]
    pub purpose: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum ProviderCategory {
    Billing, Payments, BankingFeed, FeatureFlags,
    ChannelManager, Messaging, Storage, IdentityProvider, Other,
}

/// Per ADR 0007-A1; foundation-catalog-local stub. Tender displays opaque.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MinimumSpec {
    #[serde(default)]
    pub policy: Option<SpecPolicy>,
    /// Additional fields tolerated for forward-compat with ADR 0063 Phase 1.
    #[serde(flatten)]
    pub additional: HashMap<String, serde_json::Value>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub enum SpecPolicy {
    Required, Recommended, Informational,
}

/// Per-bundle slot-satisfaction view; computed from a manifest + the host's
/// known fleet state. Q6 v1: only provider-requirement counts are computed
/// (module satisfaction is informational — tier-1 blocks are always satisfied
/// by definition per the three-tier vocabulary per Q1 ratification).
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct BundleSlotSatisfaction {
    pub bundle_key: String,
    pub total_provider_requirements: u32,
    pub required_provider_requirements: u32,
    /// In Q6 v1, all provider requirements are surfaced as "unknown" — tender
    /// does not yet probe provider-instance health. Resolves H4.
    pub unknown_satisfaction: u32,
    pub satisfied: u32,
    pub unsatisfied: u32,
}

#[derive(Debug, thiserror::Error)]
pub enum BundleReadError {
    #[error("manifest directory not found at {0}")]
    DirNotFound(PathBuf),
    #[error("io error: {0}")]
    Io(#[from] std::io::Error),
    #[error("json parse error in {path}: {source}")]
    Parse { path: PathBuf, source: serde_json::Error },
}

pub struct BundleManifestReader {
    manifest_dir: PathBuf,
}

impl BundleManifestReader {
    /// Construct from an explicit manifest directory. Production callers use
    /// `from_fleet_layout()` which derives the path from $HOME + the fleet's
    /// sibling-repo convention. Resolves H2.
    pub fn new(manifest_dir: PathBuf) -> Self { Self { manifest_dir } }

    /// Derive manifest-dir from $HOME / fleet sibling-repo layout. Returns
    /// $HOME/Projects/Harborline-Software/shipyard/packages/foundation-catalog/Manifests/Bundles.
    /// Resolves H2 — fleet sibling-repo convention.
    pub fn from_fleet_layout() -> Result<Self, BundleReadError> {
        let home = std::env::var("HOME").map_err(|_| BundleReadError::DirNotFound("$HOME unset".into()))?;
        let dir = PathBuf::from(home)
            .join("Projects/Harborline-Software/shipyard/packages/foundation-catalog/Manifests/Bundles");
        if !dir.exists() { return Err(BundleReadError::DirNotFound(dir)); }
        Ok(Self::new(dir))
    }

    /// Read all *.bundle.json files in the manifest directory. Returns
    /// (manifests, parse_errors) — partial-success: parse errors do not
    /// fail the whole read; the UI surfaces per-bundle errors.
    pub fn read_all(&self) -> Result<(Vec<BusinessCaseBundleManifest>, Vec<BundleReadError>), BundleReadError> {
        let mut manifests = Vec::new();
        let mut errors = Vec::new();
        for entry in std::fs::read_dir(&self.manifest_dir)? {
            let entry = entry?;
            let path = entry.path();
            if !path.is_file() { continue; }
            if path.extension().and_then(|s| s.to_str()) != Some("json") { continue; }
            let text = std::fs::read_to_string(&path)?;
            match serde_json::from_str::<BusinessCaseBundleManifest>(&text) {
                Ok(m) => manifests.push(m),
                Err(e) => errors.push(BundleReadError::Parse { path, source: e }),
            }
        }
        Ok((manifests, errors))
    }

    /// Compute slot-satisfaction view for a single manifest. Q6 v1: all
    /// provider requirements surface as "unknown" — see H4.
    pub fn slot_satisfaction(&self, m: &BusinessCaseBundleManifest) -> BundleSlotSatisfaction {
        let total = m.provider_requirements.len() as u32;
        let required = m.provider_requirements.iter().filter(|r| r.required).count() as u32;
        BundleSlotSatisfaction {
            bundle_key: m.key.clone(),
            total_provider_requirements: total,
            required_provider_requirements: required,
            unknown_satisfaction: total,
            satisfied: 0,
            unsatisfied: 0,
        }
    }
}
```

#### 2.2.3 Tauri commands (`commands.rs`)

Two new Tauri commands (signatures shown in TypeScript projection — Rust signatures are the obvious mirror):

```ts
// TypeScript projection (what the React component invokes)
invoke<BusinessCaseBundleManifest[]>('get_bundle_manifests'): Promise<BusinessCaseBundleManifest[]>
invoke<BundleSlotSatisfaction>('get_bundle_slot_satisfaction', { bundleKey: string }): Promise<BundleSlotSatisfaction>
```

```rust
// Rust signatures
#[tauri::command]
pub async fn get_bundle_manifests() -> Result<Vec<bundles::BusinessCaseBundleManifest>, String> {
    let reader = bundles::BundleManifestReader::from_fleet_layout()
        .map_err(|e| e.to_string())?;
    let (manifests, errors) = reader.read_all().map_err(|e| e.to_string())?;
    if !errors.is_empty() {
        // Log per-bundle parse errors but return successfully-parsed manifests.
        for err in &errors { eprintln!("[tender bundles] parse warning: {}", err); }
    }
    Ok(manifests)
}

#[tauri::command]
pub async fn get_bundle_slot_satisfaction(bundle_key: String) -> Result<bundles::BundleSlotSatisfaction, String> {
    let reader = bundles::BundleManifestReader::from_fleet_layout()
        .map_err(|e| e.to_string())?;
    let (manifests, _errors) = reader.read_all().map_err(|e| e.to_string())?;
    let manifest = manifests.iter().find(|m| m.key == bundle_key)
        .ok_or_else(|| format!("bundle '{}' not found", bundle_key))?;
    Ok(reader.slot_satisfaction(manifest))
}
```

**On error handling:** Tauri commands return `Result<T, String>` (existing tender convention — see `restart_signal_bridge`, `collect_diagnostics`). The error type is stringified at the Tauri boundary; richer error shapes are out of Q6 scope.

**On caching:** Q6 v1 reads from disk on EVERY command invocation. With 5 manifests at <10KB each, this is sub-millisecond. If the panel grows to expensive computation (e.g., real provider-instance probing in a future revision), introduce caching then. Resolves part of H3.

**On file-watch:** Q6 v1 does NOT watch the manifest directory for changes. The panel re-reads on each panel-open. Resolves part of H3.

#### 2.2.4 React UI (`BundlesPanel.tsx` + `BundleHealthBadge.tsx`)

A new panel reachable from the tender menubar navigation, sibling to fleet-services / system-stats / devices panels.

**Top-level view** — bundle inventory:

```
┌─────────────────────────────────────────────┐
│ Bundles (5)                                 │
├─────────────────────────────────────────────┤
│ • Property Management                v0.1.0 │
│   Operations / Draft               [─/─/─] │ ← BundleHealthBadge: satisfied/unknown/unsatisfied
│                                             │
│ • Asset Management                   v0.1.0 │
│   Operations / Draft               [─/─/─] │
│                                             │
│ • Project Management                 v0.1.0 │
│   ...                                       │
└─────────────────────────────────────────────┘
```

**Drill-down view** — clicking a bundle expands or replaces with:

```
┌─────────────────────────────────────────────┐
│ ← Bundles / Property Management              │
├─────────────────────────────────────────────┤
│ Version: 0.1.0                              │
│ Category: Operations                         │
│ Status: Draft                                │
│ Maturity: Scaffold                          │
│                                             │
│ Required modules (10)                       │
│   • sunfish.blocks.workflow                 │
│   • sunfish.blocks.forms                    │
│   • ... (collapsed scroll)                  │
│                                             │
│ Provider requirements (n)                   │
│   • Payments     [required]   ? unknown     │
│   • BankingFeed  [required]   ? unknown     │
│   ...                                       │
│                                             │
│ Editions: lite, standard, enterprise        │
│ Deployment modes: Lite, SelfHosted, ...     │
│                                             │
│ [requirements: present — display opaque]    │  ← only if !== null
└─────────────────────────────────────────────┘
```

**BundleHealthBadge** — small pill showing 3 counts: satisfied (green), unknown (gray), unsatisfied (red). Q6 v1: all unknown, so all bundles show gray pill with the provider-requirement count. Resolves H4 visually.

**Import statements:**

```ts
import { invoke } from '@tauri-apps/api/core';
import type {
  BusinessCaseBundleManifest,
  BundleCategory,
  BundleStatus,
  ProviderRequirement,
} from '@sunfish/contracts';
// or via tender-local re-export:
// import type { BusinessCaseBundleManifest, ... } from '../types/bundles';
```

#### 2.2.5 What PR B does NOT do

- Does NOT modify `tender/apps/desktop/src-tauri/src/telemetry.rs` — fleet-services surface stays as-is. Bundle manifests + fleet-services are separate concerns.
- Does NOT retire any flight-deck HTTP calls (`emergency_stop`, `detect_flight_deck`). See H5.
- Does NOT probe real provider-instance health (all marked "unknown"). See H4.
- Does NOT support multi-bundle activation, bundle install/uninstall, or any write-path. Read-only display.
- Does NOT introduce file-system watching. Panel re-reads on each open (sub-ms cost).
- Does NOT support arbitrary manifest-dir paths via configuration. Hard-coded fleet-layout discovery + clean error if not found. Resolves H2.
- Does NOT evaluate `requirements: MinimumSpec` against the host. Display opaque only.
- Does NOT add a notification path for bundle-manifest drift (sibling to `notifications.rs`'s service-state-transition notifier). Future revision.

#### 2.2.6 Halt conditions BEFORE PR B opens

H3, H4, H5 (see §5). H3 is most-pivotal (manifest-discovery semantics — Tier-3 source-of-truth question).

#### 2.2.7 Acceptance criteria

- [ ] `tender/apps/desktop/package.json` declares `"@sunfish/contracts": "file:../../../shipyard/packages/contracts"`
- [ ] `pnpm install` in `tender/apps/desktop/` succeeds with the new dependency resolving
- [ ] `cargo build --manifest-path tender/apps/desktop/src-tauri/Cargo.toml` clean
- [ ] `pnpm tauri dev` launches with the bundles panel reachable from the menubar
- [ ] `BundlesPanel.tsx` renders all 5 existing bundle manifests
- [ ] Clicking a bundle drills down to per-bundle detail
- [ ] Provider-requirements list renders with "unknown" badges
- [ ] If manifest-dir is missing (e.g., shipyard not cloned at sibling), the panel renders a clear error state (not a crash) — fleet-layout discovery failure tested
- [ ] No regression to fleet-services / system-stats / devices / log-tail / notification panels
- [ ] `cargo test --manifest-path tender/apps/desktop/src-tauri/Cargo.toml` passes (unit tests for `BundleManifestReader::read_all` against the 5 disk fixtures)
- [ ] CI green on tender repo

### 2.3 PR ordering + sequencing

PR A and PR B are sequential, NOT parallel:

1. **PR A lands first** (shipyard). After auto-merge to `shipyard/main`, `@sunfish/contracts` exposes `BusinessCaseBundleManifest` etc.
2. **PR B opens** (tender). Tender's `file:` ref points at `shipyard/packages/contracts/` (sibling layout); the export is available because PR A landed.

Why sequential: tender's PR B references `@sunfish/contracts` types that don't exist until PR A lands. Authoring PR B against a pre-merge PR A branch is technically possible but brittle — better to land PR A, then author PR B from main.

PR B author has ~one day after PR A lands to ship. If po-mac is saturated, an Engineer-corps subagent can pick up PR B from the PR A baseline (the spec is mechanical enough — Rust serde mirror + Tauri command pair + React panel — that subagent dispatch is viable).

### 2.4 PR C (optional refinement — recommend deferring)

**Title (if needed later):** `feat(desktop): Q6 v2 — flight-deck plugin-manifest reader + tier-3 plugin health`
**Effort:** ~3-4h
**Status:** OUT OF Q6 SCOPE; mentioned for forward-routing only.

Q6 v1 reads BUNDLE manifests (configuration descriptors composing tier-1 blocks + tier-2 provider requirements). It does NOT read FLIGHT-DECK PLUGIN MANIFESTS (tier-3 capability-plugin descriptors per Q2 ratification).

When Q2 ratifies + Engineer ships the foundation-plugins substrate (`shipyard/packages/foundation-plugins/`), a follow-on PR C extends tender's bundles panel (or adds a sibling plugins panel) to read those tier-3 manifests via `@sunfish/foundation-plugins` (TypeScript) + Rust serde mirror in `tender/apps/desktop/src-tauri/src/plugins.rs`.

Q2 is post-MVP per the ratification ruling. PR C is therefore post-MVP. Mentioning it here keeps the architectural narrative coherent: tender's bundle-manifest reader + a future tender plugin-manifest reader are sibling tier-2-vs-tier-3 surfaces, not the same code path.

---

## 3. Contract freeze — what both sides agree BEFORE PR A opens

Per the directive's §3 expectations, the contract that PR A locks in MUST be agreed before PR A is authored. Halts H1 + H2 resolve the open contract questions.

### 3.1 TypeScript shape (canonical source-of-truth)

The TypeScript shape in §2.1.2 IS the contract. After PR A lands, `@sunfish/contracts.BusinessCaseBundleManifest` is the canonical TypeScript projection of the C# record (with Rust serde struct as the third mirror in PR B).

### 3.2 Rust serde struct mirror — exact field-name mapping

Rust uses `#[serde(rename_all = "camelCase")]` to consume the same JSON-on-disk shape. Every field is named in `snake_case` in Rust source, projected to `camelCase` in JSON. Nullable C# fields → `Option<T>` in Rust; collections default empty via `#[serde(default)]`.

The mapping is mechanical (no judgment calls):

| C# (record) | TypeScript (interface) | Rust (struct field) |
|---|---|---|
| `string Key { get; init; }` (required) | `key: string;` | `pub key: String,` |
| `string? Description { get; init; }` | `description?: string \| null;` | `#[serde(default)] pub description: Option<String>,` |
| `BundleCategory Category { get; init; }` | `category: BundleCategory;` | `pub category: BundleCategory,` |
| `IReadOnlyList<string> RequiredModules` | `requiredModules: string[];` | `#[serde(default)] pub required_modules: Vec<String>,` |
| `IReadOnlyDictionary<string, string> FeatureDefaults` | `featureDefaults: Record<string, string>;` | `#[serde(default)] pub feature_defaults: HashMap<String, String>,` |
| `IReadOnlyDictionary<string, IReadOnlyList<string>> EditionMappings` | `editionMappings: Record<string, string[]>;` | `#[serde(default)] pub edition_mappings: HashMap<String, Vec<String>>,` |
| `MinimumSpec? Requirements` (`JsonPropertyName("requirements")`) | `requirements?: MinimumSpec \| null;` | `#[serde(default)] pub requirements: Option<MinimumSpec>,` |
| `BundleCategory` enum | `'Operations' \| 'Diligence' \| 'Finance' \| 'Platform'` literal union | enum with `#[serde(...)]` default-string serialization (matches C# `JsonStringEnumConverter`) |

### 3.3 Tauri command signatures (request + response projection)

| Command | Request (TypeScript args) | Response (TypeScript) | Rust impl signature |
|---|---|---|---|
| `get_bundle_manifests` | `{}` | `BusinessCaseBundleManifest[]` | `async fn() -> Result<Vec<BusinessCaseBundleManifest>, String>` |
| `get_bundle_slot_satisfaction` | `{ bundleKey: string }` | `BundleSlotSatisfaction` | `async fn(bundle_key: String) -> Result<BundleSlotSatisfaction, String>` |

`BundleSlotSatisfaction` is a tender-LOCAL view-model type (defined in `bundles.rs` only — NOT exported via `@sunfish/contracts`). Per Q6 v1, the satisfaction view is computed entirely on tender's side from manifest data alone — it doesn't need to be a fleet-shared contract type until / unless other consumers want the same view.

### 3.4 File-watch semantics — load-once-per-panel-open

Per §2.2.3 + H3 resolution: Tender reads bundle manifests from disk on each Tauri command invocation. No `notify`-crate file-watcher. No process-lifetime cache. Cost is sub-millisecond for 5 manifests at <10KB. If the manifest count grows >100 or per-manifest size grows >1MB, revisit.

### 3.5 Error handling when types drift

Two drift scenarios + their dispositions:

**Drift A — JSON on disk has a field the Rust mirror doesn't know about.** Serde with default settings (no `#[serde(deny_unknown_fields)]`) silently ignores unknown fields. Forward-compat preserved per ADR 0007-A1.4 + ADR 0028-A6 council F12 verification ("unknown-key tolerance holds — older deserializers ignore the new field silently").

**Drift B — JSON on disk is missing a field the Rust mirror expects.** Required fields (`#[serde(...)]` without `default`) fail-parse → captured as `BundleReadError::Parse` → emitted to stderr as a warning + the malformed manifest is skipped from the returned list. Other manifests parse fine. The UI surfaces (in a future revision) a per-bundle "manifest could not be read" diagnostic; v1 just shows the bundles that parsed cleanly.

**Drift C — TypeScript mirror in `@sunfish/contracts` drifts from the C# canonical shape.** The fixture-roundtrip test in PR A (§2.1.3) catches this at shipyard CI time. The 5 existing `*.bundle.json` files are the test fixtures; any shape change to the C# record SHOULD update both the JSON fixtures (via the seed/template path) AND the TypeScript mirror — the test fails otherwise.

**Drift D — Rust mirror in tender drifts from `@sunfish/contracts`.** Tender's CI runs `cargo test`; the unit tests against the 5 manifest fixtures catch parse failures. There is NO compile-time link between the TypeScript types and the Rust serde struct (different language toolchains). Drift discipline relies on test coverage + the spec in §3.2 above. Resolves part of H6 if a maintainer asks "how do we keep these in sync?"

---

## 4. Patterns claimed

**No new patterns claimed.** This is a thin consumption-layer integration:

- The pnpm `file:` ref geometry is established (sunfish/ui-react precedent).
- Tauri command pairs returning serde-roundtripped types are established (existing tender commands per `commands.rs`).
- React panel with Tauri-invoke data fetch is established (existing fleet-services / devices / system-stats panels).
- ADR 0007's manifest shape is established.

If a SECOND tender↔shipyard `@sunfish/contracts` deep-consumer surface appears later (e.g., a future "tenant inventory" panel reading `@sunfish/contracts.tenant` types), revisit and consider proposing a candidate pattern at that point (probably along the lines of "tender deep-consumes shipyard TS contracts via file: ref + Rust serde mirror"). Q6 is single-instance; no pattern proposal warranted.

---

## 5. Halt conditions — pre-spec'd for Admiral resolution before PR open

Five halts identified. H1 + H2 gate PR A (must resolve first). H3 + H4 gate PR B. H5 is forward-routing — explicitly punted but documented.

### Halt H1 (BLOCKING PR A) — `BusinessCaseBundleManifest` TypeScript export name + namespacing

**Question:** Should `BusinessCaseBundleManifest` + its supporting types live in `@sunfish/contracts/bundles.ts` (as proposed in §2.1) flat-exported, or under a `bundles.*` sub-namespace, or in a separate package `@sunfish/contracts-bundles`?

**Options:**

| Option | Geometry | Trade-off |
|---|---|---|
| **H1.A — flat in `bundles.ts` (RECOMMENDED)** | `import { BusinessCaseBundleManifest } from '@sunfish/contracts'` | Simple; matches existing flat-export of property/accounting/tenant/sync; export-collision risk is low (the names are distinctive). |
| H1.B — sub-namespaced | `import { Bundles } from '@sunfish/contracts'` then `Bundles.BusinessCaseBundleManifest` | More structured but inconsistent with the existing flat-export shape. |
| H1.C — separate package `@sunfish/contracts-bundles` | New pnpm package | Heavy; only worth it if bundle types churn independently — unlikely. |

**ONR recommendation:** H1.A. Consistent with the existing 4 namespaces (property / accounting / tenant / sync) already flat-exported. The names `BusinessCaseBundleManifest`, `BundleCategory`, `BundleStatus`, `DeploymentMode`, `ProviderCategory`, `ProviderRequirement`, `MinimumSpec` are distinctive enough that flat export poses negligible name-collision risk.

**Resolution authority:** Admiral can resolve unilaterally; no CIC gate required. ONR-default = H1.A unless Admiral routes otherwise.

### Halt H2 (BLOCKING PR B) — Bundle-manifest discovery path: filesystem vs embedded resource vs HTTP

**Question:** Where does tender READ bundle manifests from at runtime?

**Background:** ADR 0007 ships bundles as EMBEDDED JSON RESOURCES inside `Sunfish.Foundation.Catalog` (loaded into the C# assembly via `EmbeddedResource` in `.csproj`). Non-.NET consumers (tender) cannot directly access embedded resources.

The same JSON files DO exist as source on disk at `shipyard/packages/foundation-catalog/Manifests/Bundles/*.bundle.json` — that's where the C# build copies them from. Tender CAN read those source files directly via filesystem, but doing so couples tender to the fleet sibling-repo layout (`shipyard/` must be a sibling clone of `tender/`).

**Options:**

| Option | Geometry | Trade-off |
|---|---|---|
| **H2.A — filesystem via fleet-layout discovery (RECOMMENDED)** | `BundleManifestReader::from_fleet_layout()` derives `$HOME/Projects/Harborline-Software/shipyard/packages/foundation-catalog/Manifests/Bundles/` | Zero new substrate; works today; explicit error if shipyard not sibling-cloned. Matches existing tender fleet-layout patterns (`telemetry.rs::read_aspire_token` uses `$HOME/.microsoft/usersecrets/sunfish-bridge-apphost/` — same pattern). DOWNSIDE: dev-machine-shape-coupled; not portable to a packaged tender release where the user doesn't clone shipyard. |
| H2.B — bundle the JSON manifests inside the tender app at build-time | Tender's build script copies `shipyard/packages/foundation-catalog/Manifests/Bundles/*.bundle.json` into `tender/apps/desktop/resources/bundles/` at build time; tender reads from its own resources at runtime | Portable; survives a packaged release. DOWNSIDE: introduces a build-time fleet-coupling step; requires either a pre-build script or a Vite plugin; bundles are frozen at build-time (manifest changes on shipyard don't reach tender until rebuild). |
| H2.C — HTTP from Bridge or flight-deck | tender calls an HTTP endpoint (TBD where) that returns the manifests | Contradicts the Q6 ratification (Q6 SPECIFICALLY adopted deep over HTTP-hop). Rule out. |
| H2.D — bundle-manifest npm package | `@sunfish/bundle-catalog-data` ships the JSON in an npm package; tender depends on it | New substrate; mirrors flight-deck's plugin-manifest pattern. Larger lift; arguably aligned with Q2 post-MVP promotion direction but coupled to a different ratification. |

**ONR recommendation:** H2.A for Q6 v1. Rationale:
- Tender's existing fleet-layout patterns (Aspire token read; AppHost-restart launching dotnet from `$HOME/Projects/Harborline-Software/signal-bridge/...`) already assume sibling-repo layout. H2.A continues this assumption.
- Q6 v1 is dev-machine-focused — tender is a developer fleet tool, not a packaged end-user release. The sibling-clone assumption is met.
- H2.B becomes the right answer when tender ships to non-fleet end-users (post-MVP). Re-evaluate at that point. The Rust reader from H2.A is a 5-line edit to point at a packaged resource dir instead of `$HOME` — refactor cost minimal.
- H2.C ruled out by Q6 ratification.
- H2.D conflates Q6 with Q2; defer.

**Resolution authority:** Admiral can resolve unilaterally. ONR-default = H2.A. If Admiral wants portable-release-ready from day one, escalate to CIC for H2.B authorization (~+2h dev cost for build-script wiring; out of the 1-day Q6 budget).

### Halt H3 (BLOCKING PR B) — Refresh cadence: load-once vs poll vs file-watch

**Question:** How does tender stay in sync with bundle-manifest changes on disk during a tender session?

**Options:**

| Option | Geometry | Trade-off |
|---|---|---|
| **H3.A — load on each panel-open (RECOMMENDED)** | Tauri command re-reads disk on every invocation; React panel calls the command on mount | Sub-ms cost for 5 manifests; no caching/staleness logic; new manifests appear when user navigates back to the panel. |
| H3.B — load-once-per-session (cache in Rust) | Read on first Tauri-command invocation; cache in `Arc<Mutex<Vec<Manifest>>>`; subsequent calls return cached | Slightly more code; saves the disk re-read but disk-read is sub-ms anyway; user MUST restart tender to see manifest changes (poor dev-UX). |
| H3.C — file-watch via `notify` crate | Background tokio task watches the manifest dir; emits Tauri event on change; UI re-fetches | More moving parts; reqwest of new dep; CAN integrate with the existing `notifications.rs` watcher pattern. Useful in a future v2 but overkill for Q6 v1. |
| H3.D — poll on a timer | Re-read every N seconds | Worst of all worlds — bandwidth-of-disk cost without the responsiveness of file-watch. Rule out. |

**ONR recommendation:** H3.A for Q6 v1. The disk-read is so cheap it's not worth caching; the UX win of "manifest changes appear when you navigate back" exceeds any cost. H3.C is a viable v2 if a future workflow demands real-time drift detection (e.g., during a bundle-authoring session where the user is actively editing the JSON files in another editor).

**Resolution authority:** Admiral can resolve unilaterally. ONR-default = H3.A.

### Halt H4 (BLOCKING PR B) — Provider-requirement satisfaction probing: how deep does Q6 v1 go?

**Question:** When a bundle declares `providerRequirements: [{category: "Payments", required: true}]`, does tender's Q6 v1 SURFACE the requirement only, or also PROBE whether a payments provider is configured + healthy?

**Background:** Probing requires tender to know:
1. Where is the provider registry? (Today: not centralized — Bridge holds tenant-scoped provider config; flight-deck holds capability-plugin registry per Q2 promotion path.)
2. How does tender query the registry without HTTP? (It mostly can't — Bridge is HTTP-only; flight-deck is HTTP-only.)
3. What's the health-check protocol per provider category?

**Options:**

| Option | Geometry | Trade-off |
|---|---|---|
| **H4.A — surface only (RECOMMENDED for Q6 v1)** | Tender lists provider-requirements per bundle as "unknown" — counts only; no probing | Honest about what tender can determine today; no new HTTP-hop indirection (preserves Q6's "deep" posture); ~0 extra lift. |
| H4.B — probe Bridge HTTP for tenant-scoped provider config | Tender calls Bridge `/api/v1/admin/providers` (TBD existence) | Reintroduces HTTP-hop; contradicts Q6's deep posture; depends on Bridge endpoints that may not exist. |
| H4.C — probe flight-deck HTTP for capability-plugin registry | Tender calls flight-deck `/api/v1/plugins` (TBD existence) | Tier-3 plugin-registry is Q2's substrate; conflates Q6 with Q2; defer. |
| H4.D — provider-instance probe local-only | Tender pgrep for known provider processes (e.g., Stripe webhook handler) | Provider-category → process-name mapping is fragile + tenant-scope-blind; not the right approach. |

**ONR recommendation:** H4.A for Q6 v1. Surfacing "unknown" with a clear visual cue (gray pill, "unknown" label) is honest about what tender can determine today. Real satisfaction probing becomes valuable WHEN Q2's plugin-substrate ships + WHEN Bridge exposes a provider-config read endpoint — both post-MVP. Q6 v1 lays the UI groundwork; v2 fills in the probing.

**Resolution authority:** Admiral can resolve unilaterally. ONR-default = H4.A.

### Halt H5 (NON-BLOCKING; forward-routing) — flight-deck HTTP path retirement timing

**Question:** When does tender retire its existing flight-deck HTTP calls (`telemetry.rs::detect_flight_deck` health probe at `localhost:3080/health`; `commands.rs::emergency_stop` POST to `localhost:3080/api/admin/emergency-stop`)?

**Disposition:** **NOT IN Q6 SCOPE.** Q6 ADDS a bundle-manifest reader; it does NOT modify or retire the existing flight-deck-HTTP fleet-services surface. The flight-deck HTTP calls are unrelated to bundle manifests (they probe flight-deck *runtime state*, not bundle *configuration*). They can coexist indefinitely.

When Q2 (flight-deck plugin-manifest promotion to shipyard substrate) ships, a separate routing decision should be made about whether tender's flight-deck-health probe migrates from HTTP to embedded substrate. That's a Q2 follow-on, not Q6's concern.

**Resolution authority:** Documented for Admiral awareness. No action required for Q6 PRs.

### Halt H6 (LOW-PRIORITY; documentation-only) — Drift-discipline guidance

**Question:** Where does the "C# canonical → TypeScript mirror → Rust serde mirror" drift-discipline reside as durable knowledge?

**Disposition:** Add a section to `shipyard/packages/contracts/README.md` (or the bundles.ts file header docblock) naming the canonical C# source-of-truth + the fixture-roundtrip test as the drift detector. Trivial documentation cost; no halt. ONR recommends folding into PR A's README touch.

**Resolution authority:** ONR can resolve in PR A authoring time. No Admiral gate.

---

## 6. Forward-routing

### 6.1 po-mac picks up PR A and PR B

Per Q6 ratification §sequencing, **po-mac is the primary executor**. After Admiral resolves H1 + H2:

1. **po-mac authors PR A** (shipyard contracts extension) — ~3-4h. Lands on shipyard `main` via auto-merge.
2. **po-mac authors PR B** (tender bundle-manifest reader + UI) — ~4-6h. Resolves H3 + H4 disposition first (ONR-recommended defaults H3.A + H4.A — Admiral confirms or routes otherwise).
3. **po-mac files status beacons** on each PR open + each PR merge per po-mac standing protocol.

### 6.2 Fallback — subagent dispatch if po-mac saturated

The directive's §"Estimated lift" notes "or subagent if po-mac saturated". Mechanical fit for subagent dispatch:

- **PR A (shipyard) — Sonnet 4.6 + medium effort, mechanical scope.** Reading the canonical C# record, writing the TypeScript mirror, writing the fixture-roundtrip test. No judgment calls once H1 + H2 resolve.
- **PR B (tender) — Sonnet 4.6 + medium effort, mechanical scope.** Per the §2.2 spec, every Rust module, Tauri command, and React component is fully specified. Sonnet can execute the spec end-to-end.

Engineer-corps subagent dispatch authority per Engineer's CIC-ratified tech-corps charter (Engineer can dispatch the work). Routing: Admiral can authorize subagent fallback if po-mac's queue (Mac substrate slack + tender M6 Feature 3 gate-watch + iOS Field Capture W#23.x follow-ups) saturates first.

### 6.3 PR-merge dependency

Strict ordering: PR A (shipyard) merges first; PR B (tender) opens after PR A is on `main`. PR B's `file:` ref MUST resolve against PR-A-merged shipyard for tender's `pnpm install` to succeed.

### 6.4 Post-merge follow-ups (out of Q6 scope)

After both PRs merge, the following NOT-IN-Q6 items become viable:

- **Q2 plugin-substrate promotion** (separate Stage-05 hand-off, Engineer-led, post-MVP) — adds `@sunfish/foundation-plugins` substrate; tender's bundles panel grows a sibling plugins panel.
- **Provider-requirement probing** (post Q6 v1) — when Bridge exposes a provider-config endpoint, tender's H4 surface gains real satisfaction counts.
- **File-watch refresh** (post Q6 v1) — `notify` crate integration if real-time drift detection becomes desirable.
- **Tender package-mode** (post-MVP) — when tender ships as a non-fleet end-user binary, H2.A switches to H2.B.
- **MinimumSpec evaluation** — when ADR 0063 Phase 2 wiring ships, tender can evaluate `requirements` against the host's MissionEnvelope and surface install-blocking diagnostics.

---

## 7. Stage-05 adversarial-review framework — NOT REQUIRED

Per ADR 0093 Rev 4 ("adversarial-review framework only when new patterns claimed"), Q6 claims no new patterns (§4). No adversarial framework section required.

If the spec gains a candidate pattern during PR authoring (e.g., a second tender↔shipyard contracts wire emerges and warrants pattern proposal), revisit at that point. Today: thin consumption layer; no pattern proposal.

---

## 8. Summary

- 1 ratification trace (CIC §Q6 2026-05-25T22:00Z) → 1 spec → 2 PRs (1 shipyard contracts extension + 1 tender Rust/UI integration).
- 0 new patterns claimed.
- 6 halts identified — 2 blocking PR A, 2 blocking PR B, 1 forward-routing (non-blocking), 1 documentation (resolvable in PR A authoring).
- ~1-day total dev (per Q6 ratification estimate) — split as ~3-4h PR A + ~4-6h PR B.
- po-mac is primary executor; Sonnet 4.6 subagent fallback viable for both PRs if po-mac saturated.
- Q6 v1 is DEEP for bundle-manifest READS only. Provider-requirement probing + flight-deck HTTP retirement + tier-3 plugin-manifest surfaces are post-MVP follow-ups, intentionally out of scope.

---

*Authored by ONR 2026-05-25T2300Z. Ready for Admiral halt resolution (H1 + H2) so po-mac can convert pre-flight survey to PR A authoring.*
