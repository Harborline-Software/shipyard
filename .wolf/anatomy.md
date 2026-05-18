# shipyard — Anatomy

Per-repo OpenWolf anatomy. Lists files + 2-3 line descriptions + token estimates.

## Top-level layout

- `packages/` — shared .NET packages (domain libs, UI adapters, design tokens)
- `accelerators/` — accelerator apps and Bridge layer
- `apps/` — app-level projects (docs, marketing, etc.)
- `apps/docs/` — Docusaurus documentation site for all blocks
- `apps/docs/blocks/` — per-block doc sections (toc.yml + overview.md stubs)
- `_shared/` — fleet-wide engineering, product, and design docs
- `_shared/engineering/` — standing patterns, ADR index, engineering guidelines
- `icm/` — ICM pipeline artifacts (handoffs, state, templates)
- `icm/_state/handoffs/` — per-workstream ICM handoff files (stage 00–08)
- `icm/_templates/` — ICM handoff templates

## apps/docs/blocks/

Central docs section for all Sunfish application blocks.

- `toc.yml` — master table of contents for docs site; 42 block entries as of 2026-05-18. ~150 tokens
- `blocks-presence/overview.md` — stub: Presence block (online status, occupancy) ~30 tokens
- `docs-core/overview.md` — stub: Docs Core block ~30 tokens
- `docs-dam/overview.md` — stub: Docs DAM block (digital asset management) ~30 tokens
- `docs-retention/overview.md` — stub: Docs Retention block ~30 tokens
- `docs-signing/overview.md` — stub: Docs Signing block (e-signatures) ~30 tokens
- `docs-templates/overview.md` — stub: Docs Templates block ~30 tokens
- `docs-wiki/overview.md` — stub: Docs Wiki block ~30 tokens
- `financial-ap/overview.md` — stub: Accounts Payable block ~30 tokens
- `financial-payments/overview.md` — stub: Financial Payments block ~30 tokens
- `foundation-featuremanagement/overview.md` — stub: Feature Management block ~30 tokens
- `foundation-wayfinder/overview.md` — stub: Wayfinder / navigation block ~30 tokens
- `receipts/overview.md` — stub: Receipts block ~30 tokens
- `reports/overview.md` — stub: Reports block ~30 tokens

## _shared/engineering/

- `standing-approved-patterns.md` — fleet-wide pre-approved standing patterns (pattern-001 through pattern-011+); canonical reference for pre-authorized work. ~300 tokens

## icm/_state/handoffs/

Large collection of per-workstream ICM stage handoff files. Naming: `<workstream-slug>-stage<NN>-<description>.md`. Most files ~100–400 tokens. As of 2026-05-18, all SunfishSoftware/ legacy path refs have been swept (rounds 3–5; PRs shipyard#23, #24).

## icm/_templates/

- `handoff-stage06.md` — canonical Stage 06 implementation handoff template. ~200 tokens
