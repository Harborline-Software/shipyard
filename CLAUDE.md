# Claude Instructions for shipyard

Shipyard — framework-agnostic application platform; hosts all shared packages, design tokens, UI adapters, tooling, ADRs, ICM, _shared engineering + product + design.

## Fleet context

This repo is part of the **Harborline Software fleet**. Fleet-level conventions, agent definitions, and rules live at the parent folder `/Users/christopherwood/Projects/Harborline-Software/CLAUDE.md`.

Always read the parent fleet CLAUDE.md first for cross-fleet protocols (coordination inbox, pre-authorization, standing patterns, effort policy).

## Repo-specific context

This repo's anatomy lives in `.wolf/anatomy.md` (OpenWolf). Check that before reading project files.

For repo-specific architecture, conventions, and testing — see folders within this repo:
- `docs/` — user docs (if present)
- `README.md` — repo overview

## Cross-repo dependencies

If this repo depends on shipyard packages (.NET ProjectReference OR pnpm workspace), see `README.md` for sibling-folder layout requirement: `shipyard/` must be cloned at sibling level for builds to resolve.

## Migration history

Migrated 2026-05-17 from `Harborline-Software/` legacy layout. See parent `MIGRATION.md` for details.
