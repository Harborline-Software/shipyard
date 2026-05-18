---
id: 90
title: ui-react Distribution Strategy (commit dist/ for cross-repo file: consumers)
status: Proposed
date: 2026-05-17
tier: tooling
pipeline_variant: sunfish-feature-change

concern:
  - distribution
  - ui
  - supply-chain
  - tooling

enables:
  - cross-repo-ui-react-consumption-without-prepare-hook
  - deterministic-cold-and-warm-install-behavior

composes:
  - 14   # Adapter parity policy
  - 30   # React adapter scaffolding
  - 77   # Shared design system
  - 86   # Anchor Tauri-React product surface (consumer)

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

amendments: []
---

# ADR 0090 — ui-react Distribution Strategy

**Status:** Proposed
**Date:** 2026-05-17
**Resolves:** task #34 (FED PR #10 `prepare` script fragility after 2026-05-17 multi-repo restructure)

---

## Context

The 2026-05-17 Harborline-Software multi-repo restructure split the fleet into sibling repositories under `/Users/christopherwood/Projects/Harborline-Software/`. The previous single-repo layout let pnpm workspaces resolve `@sunfish/ui-react` natively. Post-split, two consumer apps now live in a different git repository from the `ui-react` package source:

- `sunfish/apps/desktop/package.json` (`@sunfish/anchor-tauri`) — Tauri-shelled React surface (ADR 0086).
- `sunfish/apps/web/package.json` (`@sunfish/anchor-react`) — web React surface, RETIRED in W#60 Phase 3 but still installed.

Both pin the package via a relative `file:` specifier:

```json
"@sunfish/ui-react": "file:../../../shipyard/packages/ui-react"
```

The `shipyard/packages/ui-react/package.json` `files` field declares `["dist", "README.md"]`. The package's `main`, `module`, and `types` all point inside `dist/`. The consumer's `node_modules/@sunfish/ui-react/` therefore needs a populated `dist/` directory at resolve time, or every import fails.

FED's PR #10 (shipped today) restored installability by adding a `prepare` script:

```json
"scripts": {
  "prepare": "npm run build",
  "build": "vite build"
}
```

This works in the common case but is fragile. The npm `prepare` lifecycle for a `file:` dependency runs **only on cold install** — when the consumer's `node_modules/@sunfish/ui-react/` is being created or replaced. On a warm install (when npm decides nothing about that entry changed), `prepare` does not re-execute. Two failure modes follow:

1. **Stale `dist/` after source edits.** A developer edits `shipyard/packages/ui-react/src/*`, then runs `npm install` in `sunfish/apps/desktop/`. npm sees the `file:` target unchanged at the path level, skips the link replacement, and `prepare` never runs. The consumer keeps importing the previous `dist/`.
2. **Cold-install ordering coupling.** A fresh clone of `sunfish` without a prior `shipyard/packages/ui-react/dist/` directory must build ui-react first or as part of the install — but `prepare` runs **inside the consumer's install**, building into the source tree. If shipyard's `node_modules` is missing (e.g., the developer hasn't `npm install`-ed shipyard yet), the vite build fails with cryptic missing-devDep errors.

The current 2-consumer scope makes the failure footprint small but the diagnostic cost high — a build error during `npm install` in the consumer is several layers removed from the actual fault.

---

## Decision drivers

- **Cold/warm install parity.** Behavior must not depend on which order developers ran `npm install` across repos.
- **Diagnostic locality.** When ui-react is broken, the failure should surface in shipyard, not three directories away in a consumer's install log.
- **Cheap to reverse.** The fleet is still pre-1.0; the right long-term answer (private registry) requires infrastructure investment that's not justified at 2 consumers.
- **Cross-repo reality.** Consumers and producer now live in different git histories. Solutions that assume a single workspace (Turbo, pnpm filter, npm workspaces) no longer apply without re-merging the repos.
- **Reviewability of generated artifacts.** Committed `dist/` adds bytes to git history and shows up in PR diffs. Reviewers must ignore it; CI must enforce that it matches source.
- **ADR 0014 (adapter parity) and ADR 0077 (shared design system)** both depend on ui-react being trivially consumable by every React adapter consumer. Distribution friction here taxes every downstream package.

---

## Considered options

### Option A — Keep `prepare` script + document install ordering

Status quo after FED PR #10. Add a `CONTRIBUTING.md` paragraph documenting "always `npm install` in shipyard first, then in consumer apps; if you edit ui-react source, run `npm run build` in `shipyard/packages/ui-react/` before re-installing consumers."

- **Pro:** Zero additional infrastructure. Already shipped.
- **Pro:** No artifacts in git.
- **Con:** Documentation-as-mitigation for a behavioral cliff. The failure mode (stale `dist/` on warm install) is invisible until something breaks at import time.
- **Con:** Onboarding tax: every new contributor must internalize the ordering rule.
- **Con:** Doesn't fix the cold-install case where shipyard's `node_modules` isn't yet populated.

**Verdict:** Rejected. Trades a real correctness problem for a doc that nobody reads under pressure.

### Option B — Commit built `dist/` to git + CI gate asserting no-diff [RECOMMENDED]

Build `dist/` locally, commit it to `shipyard` git history under `packages/ui-react/dist/`. Consumers resolve the `file:` dependency and immediately have a usable artifact — no `prepare`, no ordering, no warm/cold distinction.

A CI job in shipyard runs on every PR touching `packages/ui-react/`:

1. `npm ci` in `shipyard/packages/ui-react/`
2. `npm run build`
3. `git diff --exit-code -- packages/ui-react/dist/`

The diff gate fails the PR if the author forgot to rebuild. Remove the `prepare` script (it would otherwise re-run inside consumer installs and overwrite the committed artifact unpredictably).

- **Pro:** Cold and warm installs behave identically — `dist/` is just files in the consumer's `node_modules/@sunfish/ui-react/dist/`.
- **Pro:** Diagnostic locality. If `dist/` is broken, the PR that broke it failed CI in shipyard, not in a consumer's install log.
- **Pro:** Removes the build-during-install footgun (vite + dts running inside a consumer's `npm install`).
- **Pro:** Reversible — when we move to Option C, deleting `dist/` from git is one commit.
- **Con:** Generated artifacts in git. `dist/` is currently ~tens of KB (CJS + ESM + sourcemaps + .d.ts bundle); not large but non-zero.
- **Con:** PR diffs include the rebuilt `dist/`. Reviewers must treat it as auto-generated. Mitigated by `.gitattributes linguist-generated=true` and a CODEOWNERS exclusion if needed.
- **Con:** Two-step contributor workflow: edit source, then `npm run build` before commit. CI catches the omission.

**Verdict:** RECOMMENDED. Cheapest robust fix for the current 2-consumer scope.

### Option C — Publish to a private npm registry / GitHub Packages

Publish `@sunfish/ui-react` to GitHub Packages (or a private npm registry). Consumers pin a version: `"@sunfish/ui-react": "0.1.0-alpha"`. The package is built once at publish time and consumed as a versioned tarball thereafter.

- **Pro:** Standard ecosystem pattern. Decouples producer and consumer entirely.
- **Pro:** Versioning becomes explicit — consumers can pin, upgrade deliberately, see changelogs.
- **Pro:** No generated artifacts in git.
- **Pro:** Scales to N consumers at flat cost.
- **Con:** Requires registry auth setup in every consumer repo's CI (GH Packages token, `.npmrc`, etc.).
- **Con:** Publish-on-every-change friction at the current pace of ui-react churn (~daily during cohort-1 rebind).
- **Con:** Local development of ui-react against a consumer now needs `npm link` or `yalc` or a publish-prerelease dance — the immediacy of `file:` is lost.
- **Con:** GitHub Packages has historically been awkward for scoped packages owned by an org versus a user; setup cost is real.

**Verdict:** Right long-term answer. Defer until consumer count >3 or until ui-react release cadence slows enough that publish friction is acceptable.

### Option D — Single Turbo / pnpm workspace spanning shipyard + sunfish

Re-establish a single workspace that includes both `shipyard/packages/*` and `sunfish/apps/*`. Turbo (or pnpm workspaces alone) builds ui-react before its consumers.

- **Pro:** Clean dependency graph. No `dist/` in git. No `prepare` script.
- **Pro:** Hot reload across the boundary.
- **Con:** **Directly conflicts with the 2026-05-17 multi-repo restructure.** Forming a workspace requires either a monorepo `package.json` at the parent folder (which Harborline-Software is not — it's a folder of independent git repos) or a virtual workspace tool that crosses repo boundaries (none is standard).
- **Con:** Reverses the architectural decision that just shipped today.

**Verdict:** Rejected. Re-litigating the restructure is out of scope for a distribution-strategy ADR.

---

## Decision

**Adopt Option B.** Commit `shipyard/packages/ui-react/dist/` to git. Remove the `prepare` script from `shipyard/packages/ui-react/package.json`. Add a shipyard CI job that rebuilds `dist/` and fails on diff.

Option C is the **correct long-term destination** and should be revisited once any of the revisit triggers fires. This ADR explicitly frames Option B as a 2-to-3-consumer bridge, not an end state.

### Substrate / layering notes

- `dist/` artifacts in git are treated as **build outputs**, not source. Mark `packages/ui-react/dist/**` with `linguist-generated=true` in `.gitattributes`.
- Consumers continue to resolve via `file:../../../shipyard/packages/ui-react`. No consumer-side changes required.
- The CI no-diff gate is the **load-bearing safety**. Without it, contributors will forget to rebuild and the committed `dist/` will drift from source. The gate is non-optional.

---

## Consequences

### Positive

- Cold and warm `npm install` produce identical results in consumer apps.
- ui-react build failures surface in shipyard CI, not in consumer install logs.
- Removes a build-during-install execution path (vite + dts plugin no longer run inside a consumer's lifecycle hook).
- Onboarding simplifies: clone consumer, `npm install`, done — no cross-repo ordering rule.

### Negative

- `dist/` in git increases repo size by tens of KB per change; over the project's life this accumulates. Acceptable at the current scale.
- PR diffs include rebuilt `dist/`. Mitigated by `.gitattributes` and reviewer convention.
- Contributors must `npm run build` before committing source changes. CI catches omissions.

### Trust impact / Security & privacy

- Committed `dist/` is auditable in git history — supply-chain attestation is stronger than a `prepare` script that builds opaquely on every consumer machine. Mild positive.
- No new secrets, capabilities, or trust boundaries introduced.

---

## Compatibility plan

- **Affected packages:** `shipyard/packages/ui-react/` (producer), `sunfish/apps/desktop/` and `sunfish/apps/web/` (consumers).
- **Consumer-side migration:** none. The `file:` specifier resolves to the same directory; the only difference is that `dist/` is now present at clone time.
- **Producer-side migration:**
  1. Run `npm run build` in `shipyard/packages/ui-react/`.
  2. Remove `dist/` from `.gitignore` if present; commit the built output.
  3. Remove `"prepare": "npm run build"` from `package.json`.
  4. Add `packages/ui-react/dist/** linguist-generated=true` to `shipyard/.gitattributes`.
  5. Add the no-diff CI job (see Implementation checklist).
- **Rollback:** restore `prepare` to `package.json`, `git rm -r packages/ui-react/dist/`, restore `dist/` to `.gitignore`. One commit, fully reversible.

---

## Implementation checklist

- [ ] Build `dist/` locally via `npm run build` in `shipyard/packages/ui-react/`.
- [ ] Verify the build outputs (`dist/index.js`, `dist/index.cjs`, `dist/index.d.ts`, sourcemaps) are present and reasonable.
- [ ] Remove `dist/` from `shipyard/.gitignore` (and any package-local `.gitignore`) if listed.
- [ ] Stage and commit `packages/ui-react/dist/` to git.
- [ ] Remove `"prepare": "npm run build"` from `shipyard/packages/ui-react/package.json`.
- [ ] Add `packages/ui-react/dist/** linguist-generated=true` and `packages/ui-react/dist/** -diff` to `shipyard/.gitattributes`.
- [ ] Add a shipyard CI workflow (or extend an existing one) with steps: checkout, `npm ci` in `packages/ui-react/`, `npm run build`, `git diff --exit-code -- packages/ui-react/dist/`.
- [ ] Add a path filter so the CI job only runs on PRs touching `packages/ui-react/src/**` or `packages/ui-react/package.json` or the workflow itself.
- [ ] Smoke-test in both consumer apps: `rm -rf node_modules && npm install` in `sunfish/apps/desktop/` and `sunfish/apps/web/`; verify `node_modules/@sunfish/ui-react/dist/` contains files and consumer `npm run build` succeeds.
- [ ] Update `shipyard/packages/ui-react/README.md` (if present) with a one-paragraph note: "Built artifacts are committed; run `npm run build` before committing source changes."

---

## Open questions

- **Sourcemaps in committed `dist/`.** vite emits `.map` files (vite.config.ts `sourcemap: true`). Acceptable to commit, or strip in a publish step? Default: commit them — they're useful for consumers debugging into ui-react. Revisit if size grows.
- **CI runner.** Shipyard CI platform per ADR 0037; the no-diff job slots into the existing workflow set. Author should confirm the exact workflow file.
- **CODEOWNERS for `dist/`.** Should `packages/ui-react/dist/` have a permissive CODEOWNERS rule so that ui-react contributors don't need separate approval for the regenerated artifact? Recommend yes.

---

## Revisit triggers

This ADR should be re-evaluated and likely superseded when **any** of the following fires:

- **A third consumer of `@sunfish/ui-react` is added** beyond `apps/desktop/` and `apps/web/`. At three consumers, the cost of "rebuild + commit + CI gate" begins to lose to the cost of "publish + version pin." Migrate to Option C.
- **`dist/` diff churn exceeds ~50% of ui-react PRs by line count.** Indicates the artifact-in-git tax is dominating reviewer attention.
- **A consumer needs to pin a specific ui-react version** different from `HEAD` (e.g., for a release branch). `file:` + committed `dist/` can't express that; a registry can.
- **GitHub Packages or another private registry is provisioned for the fleet** for any other package. ui-react should ride that infrastructure.
- **ui-react release cadence slows to weekly or less.** Publish friction (Option C's main downside) becomes acceptable.

---

## References

### Predecessor and sister ADRs

- [ADR 0014](./0014-adapter-parity-policy.md) — Adapter parity policy; ui-react is the React adapter substrate.
- [ADR 0030](./0030-react-adapter-scaffolding.md) — React adapter scaffolding; established ui-react's package shape.
- [ADR 0077](./0077-shared-design-system.md) — Shared design system; ui-react consumes design tokens that themselves cross the repo boundary.
- [ADR 0086](./0086-anchor-tauri-react-product-surface.md) — Anchor Tauri-React product surface; primary ui-react consumer.

### Roadmap and specifications

- 2026-05-17 multi-repo restructure (`MIGRATION.md` at fleet root).
- FED PR #10 (`shipyard/packages/ui-react/` `prepare` script — the proximate trigger for this ADR).

### Existing code / substrates

- `shipyard/packages/ui-react/package.json`
- `shipyard/packages/ui-react/vite.config.ts`
- `sunfish/apps/desktop/package.json`
- `sunfish/apps/web/package.json`

### External

- npm `prepare` lifecycle: <https://docs.npmjs.com/cli/v10/using-npm/scripts#prepare-and-prepublish>
- npm `file:` specifier semantics: <https://docs.npmjs.com/cli/v10/configuring-npm/package-json#local-paths>

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Four alternatives evaluated (status quo + doc, commit dist/, private registry, re-form workspace). The simpler alternative — Option A status quo + documentation — is explicitly rejected with reasoning. Option D (re-form workspace) is the structurally simplest fix and is rejected only because it reverses today's restructure.
- [x] **FAILED conditions / kill triggers.** Five revisit triggers named; any one fires Option C migration.
- [x] **Rollback strategy.** One-commit rollback documented in Compatibility plan: restore `prepare`, delete committed `dist/`, restore `.gitignore`.
- [x] **Confidence level.** MEDIUM — the mechanism is mainstream (many OSS packages commit `dist/` to support `file:`/git-URL installs) but the no-diff CI gate is the load-bearing safety and must be implemented correctly for the ADR's claims to hold.
- [ ] **Cited-symbol verification.** No `Sunfish.*` C# symbols cited. Package paths and `package.json` field names verified against `shipyard/packages/ui-react/package.json` and both consumer manifests as of 2026-05-17.
- [x] **Anti-pattern scan.** AP-1 (unvalidated assumptions): the warm/cold install asymmetry is validated by npm `prepare` documentation. AP-3 (vague success criteria): "cold and warm install produce identical consumer state" is observable. AP-9 (skipping Stage 0): four alternatives surveyed. AP-12 (timeline fantasy): no timeline claims made. AP-21 (assumed facts): npm lifecycle citation added.
- [x] **Revisit triggers.** Five named (consumer count, diff churn, version-pin need, registry provisioning, cadence slowdown).
- [x] **Cold Start Test.** The Implementation checklist is executable without author clarification: each step is concrete (`npm run build`, `git rm`, file edits, CI workflow addition).
- [x] **Sources cited.** npm `prepare` semantics and `file:` specifier semantics linked externally.

---

*Drafted in response to task #34 (FED PR #10 fragility). Council routing announced via `coordination/inbox/admiral-status-2026-05-17T23-30Z-ui-react-adr-0090-drafted.md`.*
