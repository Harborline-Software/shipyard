# Phase 3 — Namespace Rename Migration Plan

**Authority:** Admiral (this draft) — pending CIC ratification
**Date drafted:** 2026-05-17
**Framework:** Universal Planning Framework v1.2 — Stage 1
**Quality target:** B (5 CORE + FAILED conditions + Confidence + Cold Start)
**Predecessor:** Phase 2 closed via `/Users/christopherwood/Projects/Harborline-Software/RATIFICATION-2026-05-17.md`
**Successor:** Phase 4 (marketing rebrand — PAO workstream)

---

## 1. Context & Why

Phase 2 of the Harborline restructure (folder layout, build configuration, GitHub remotes) was ratified 2026-05-17. The `.NET` namespace tree still reads `Sunfish.*` everywhere — across 232 csproj files, 3,510+ namespace declarations (3,306 in shipyard + 74 in sunfish + 130 in signal-bridge), 367 `ProjectReference` lines, 118 `<PackageId>` declarations, 808 razor files, and analyzer-auto-wiring predicates inside `shipyard/Directory.Build.props`. Phase 3 renames the namespace tree so the platform (`shipyard/`) reads as **Harborline.*** while the ERP product (`sunfish/`) keeps `Sunfish.*` — a deliberate split that matches the post-Phase-2 brand topology.

Deferred from Phase 2 closure because the sweep is mechanically large and conflicts with six in-flight workstreams (cohort-1, W#68, W#69, W#70, W#23 P6, WS-E). Shipping mid-cohort would create unmergeable rebases.

---

## 2. Success Criteria

### Measurable outcomes (PASS)
- `dotnet build shipyard/Shipyard.slnx` — green, zero warnings beyond pre-existing NU1510/CS1591 baseline.
- `dotnet build sunfish/Sunfish.slnx` against sibling renamed shipyard — green.
- `dotnet build signal-bridge/SignalBridge.slnx` against sibling renamed shipyard — green.
- `dotnet test` test-count diff: identical pass count to pre-Phase-3 baseline. Allowed delta: 0.
- `grep -r "namespace Sunfish\." shipyard/packages` returns 0 lines outside whitelisted preservation set (see Mapping Rules §M-PRESERVE below).
- `grep -r "namespace Sunfish\." sunfish/` — preserved as `Sunfish.Anchor.*` (unchanged).
- `grep -r "namespace Sunfish\." signal-bridge/` — preserved as `Sunfish.Bridge.*` until W#82 (separate workstream) reassigns the comms mesh brand.
- All `<PackageId>` values updated and the resulting `bin/*.nupkg` artifacts carry the new identifiers.
- `shipyard/Directory.Build.props` MSBuildProjectName predicates updated; analyzer auto-wiring still fires on rename-target packages.
- All `InternalsVisibleTo` attributes (both csproj `<InternalsVisibleTo Include="…" />` and source `[assembly: InternalsVisibleTo("…")]`) match the new assembly names.
- ADR refs + ICM hand-off refs updated by sweep-aware substitution (historical fragments inside `_archive/` exempt — they are frozen content).

### FAILED conditions (kill triggers)
- **F1:** Mapping rules (§3 Assumption A1) not ratified by CIC within 48 hours of plan circulation — HALT; Phase 3 cannot start.
- **F2:** Any of the six quiescence-required workstreams (cohort-1, W#68, W#69, W#70, W#23 P6, WS-E) has unmerged changes touching csproj or namespace surfaces at the moment a phase boundary opens — HALT that phase; wait for merge.
- **F3:** Per-package rename sweep produces > 50 build errors that cannot be auto-fixed by the second tooling pass — REVERT that package PR, file blocker, skip to next package.
- **F4:** Test pass count regresses by ≥ 1 after a package PR — REVERT that PR, root-cause before resuming.
- **F5:** Timeline timeout — sweep not complete in 14 calendar days from kickoff — STOP, reconvene with CIC; partial rename is acceptable end-state (cluster-by-cluster) but a stall mid-cluster is not.

### Confidence Level
**Medium-High.** The sweep is mechanical, the codebase compiles green today (Phase 2 closure attests), and per-package PR boundaries make each step independently revertable. Risk concentrates in two narrow surfaces — the `Directory.Build.props` predicates and the cross-repo `Sunfish.Anchor` / `Sunfish.Bridge` ProjectReference lines — both enumerable and validatable.

---

## 3. Assumptions & Validation

| # | Assumption | VALIDATE BY | IMPACT IF WRONG |
|---|---|---|---|
| **A1** | Mapping target is `Sunfish.*` → `Harborline.*` for shipyard packages, with `Sunfish.Anchor.*` and `Sunfish.Bridge.*` preserved as ERP/mesh product names. **CIC ratified 2026-05-18: "Sunfish platform changes to harborline shipyard / Anchor changes to sunfish"** — confirms `Harborline.*` for shipyard, `Sunfish.Anchor.*` preserved for ERP. Signal-bridge namespace fate TBD (W#82). | Confirmed via CIC ruling 2026-05-18T03:30Z (book rebrand mapping ruling). | n/a — ratified |
| **A2** | Each package cluster (foundation, kernel, blocks, ui-*, compat-*, analyzers, federation, ingestion, providers) can rename independently because ProjectReference paths are by filesystem path (not assembly name), and within a single cluster the rename is local. | Pilot Phase 4.1 (foundation root) end-to-end first, observe whether downstream clusters need adjustment. | If wrong, every PR must touch all dependents — sweep collapses into one mega-PR. Recoverable but slower. |
| **A3** | The six quiescence workstreams (cohort-1, W#68, W#69, W#70, W#23 P6, WS-E) can each merge within 7 days of plan circulation. | QM round-status snapshots; per-workstream blocker reports in coordination inbox. | Phase 3 start slips. Acceptable. Plan is deliberately non-time-critical. |
| **A4** | `Sunfish.Anchor` (sunfish/) and `Sunfish.Bridge` (signal-bridge/) ProjectReference *paths* (`../../shipyard/packages/...`) do not change — only the *assembly names + csproj filenames* of the referenced shipyard packages change. The two consuming repos need a coordinated PR that updates the `Include=` filename and any `using Sunfish.*;` in their .cs sources. | Pilot foundation rename → run `dotnet restore` in sunfish/ and signal-bridge/ → expect 367 references to fail until consuming-side PR lands. | If wrong, consuming repos break for longer than the per-cluster window. Coordinate consumer-side PR same day as shipyard cluster PR merges. |
| **A5** | Razor `_Imports.razor` carries the bulk of namespace surface; only 3 `.razor` files have explicit `@namespace Sunfish.*`. The tooling can treat `.razor` as a near-no-op cluster. | grep verification before sweep + during sweep. | Razor breakage. Unlikely given the 3-file count. |
| **A6** | `BannedSymbols.txt` content and the `Authors=Sunfish Contributors` / `Company=Sunfish` properties in `Directory.Build.props` are orthogonal to namespace and stay as-is until Phase 4 (marketing rebrand). | grep verification; CIC sign-off on the orthogonality. | Branding inconsistency lingers; not a build risk. |
| **A7** | The 73 ADR markdown files referencing `Sunfish.*` are documentation refs (not load-bearing code paths). Sweep updates them via string-substitution; no semantic review required for the rename pass itself. | Spot-check 5 ADR diffs after sweep. | If ADRs contain code samples that other tooling parses, those samples need verified rewriting. Low probability. |

---

## 4. Phases

Each phase below is a separate PR (or PR set). All phases run **after** A1 ratification + A3 quiescence. Cluster ordering follows the dependency DAG (foundation root → kernel → blocks → consumers).

### Phase 4.0 — Quiescence gate & tooling preparation (1 PR; tooling-only)

**Scope:** No source rename. Deliverables:
- `shipyard/tooling/phase3-rename/` directory containing a Python (or `dotnet` global tool) script `rename-namespace.py` (or equivalent). Script accepts `--cluster=foundation` style args and performs:
  1. csproj rewrite: `<PackageId>`, `<RootNamespace>`, `<AssemblyName>`, file rename (`Sunfish.Foundation.csproj` → `Harborline.Foundation.csproj`), `InternalsVisibleTo Include=`, all `ProjectReference Include=` paths that point into the cluster.
  2. .cs source rewrite: identifier-level `namespace Sunfish.X` → `namespace Harborline.X`; `using Sunfish.X` → `using Harborline.X`; fully-qualified `Sunfish.X.Y.Z` → `Harborline.X.Y.Z`.
  3. .razor source rewrite (rare): `@namespace`, `@using`.
  4. .slnx / .csproj cross-cluster ProjectReference rewrite (for downstream phases).
  5. ADR + ICM doc string-substitution pass.
- The script must be **idempotent** (re-running on already-renamed content is a no-op) and must **dry-run by default** (matches Phase 2 script convention).
- Whitelist file `phase3-preserve.txt` listing namespaces that stay `Sunfish.*` (the ERP product surface — see §M-PRESERVE).

**Gate (PASS/FAIL):**
- PASS: Script dry-runs cleanly on foundation cluster, produces a diff that a human can read, builds green when applied.
- FAIL: Script can't handle one of the surfaces (e.g., generated files, source generators) → fix tooling before phase 4.1 starts.

### Phase 4.1 — Foundation root cluster (1 PR)

**Scope:** `shipyard/packages/foundation` + all `foundation-*` (32 packages). This is the dependency root — nothing depends on it being unrenamed.
- Sweep csproj files (file renames, PackageId, AssemblyName, RootNamespace).
- Sweep .cs sources (namespace + using).
- Update `shipyard/Directory.Build.props` predicates: `StartsWith('Sunfish.Foundation')` → `StartsWith('Harborline.Foundation')`; exclusion entry `Sunfish.Foundation.Integrations` → `Harborline.Foundation.Integrations`.
- Update `Shipyard.slnx` cluster entries.
- Update ADRs referencing `Sunfish.Foundation.*` symbols.

**Gate:** `dotnet build shipyard/Shipyard.slnx` green (downstream packages still reference foundation by old name — expected to fail at this isolation; build only the foundation cluster subset OR build the whole sln with the **same PR** rewriting all `ProjectReference Include="..\foundation\Sunfish.Foundation.csproj"` lines in downstream csprojs at filename level only — keeping assembly references intact via the csproj `Include=` path-only change).

**Decision point for PR shape:**
- **Option A (recommended):** One PR per cluster, but each PR includes "dependent filename-only updates" — i.e., a `blocks-engine-room` csproj's `<ProjectReference Include="..\foundation\Harborline.Foundation.csproj" />` is updated to the new filename in the foundation PR, even though `blocks-engine-room` itself stays `Sunfish.Blocks.EngineRoom` in that PR. Keeps each PR small and reviewable.
- **Option B:** One mega-PR. Rejected — too large to review or revert.

### Phase 4.2 — Kernel cluster (1 PR)

**Scope:** `shipyard/packages/kernel` + all `kernel-*` (13 packages). Depends on foundation (already renamed). Downstream: blocks, consumers.
- Same mechanics as 4.1.
- No Directory.Build.props predicate changes (no kernel-specific predicate today).

**Gate:** `dotnet build` green; kernel tests pass.

### Phase 4.3 — UI core + adapters cluster (1 PR)

**Scope:** `ui-core`, `ui-adapters-blazor`, `ui-adapters-blazor-a11y`, `ui-adapters-react`, `ui-react` (5 packages).
- csproj rewrites; .cs rewrites; razor `_Imports.razor` rewrites; 3 razor `@namespace` rewrites.

**Gate:** Razor compilation green; kitchen-sink app builds.

### Phase 4.4 — Compat shims cluster (1 PR)

**Scope:** 13 `compat-*` packages.
- Rename `Sunfish.Compat.*` → `Harborline.Compat.*`.

**Gate:** Compat-aware tests pass.

### Phase 4.5 — Blocks cluster (1 PR — large)

**Scope:** ~30 `blocks-*` packages. Depends on foundation + kernel + ui adapters (all renamed).
- csproj rewrites; .cs rewrites.
- Update `Directory.Build.props` predicate `StartsWith('Sunfish.Blocks.')` → `StartsWith('Harborline.Blocks.')`.
- 118 `InternalsVisibleTo` entries get rewritten here (most are `Sunfish.Blocks.X.Tests`).

**Gate:** `dotnet build` + `dotnet test` green for entire blocks cluster.

**Note:** Largest single PR. Reviewer should diff csproj files first, then spot-check 5 random .cs files.

### Phase 4.6 — Federation + Ingestion + Providers + Analyzers + Tooling clusters (1 PR each — 5 PRs)

**Scope:** `federation-*` (4 packages), `ingestion-*` (8 packages), `providers-*` (3 packages), `analyzers` (Sunfish.Analyzers.* — 4 internal analyzer projects under `packages/analyzers/`), `tooling` (5 `Sunfish.Tooling.*` projects under `shipyard/tooling/`).
- One PR per cluster (5 PRs total).
- Analyzers cluster: rename source generator class names if they include "Sunfish" in their type identifier (usually they don't — just namespace).
- Tooling cluster: rename `Sunfish.Tooling.LocalizationXliff.csproj` → `Harborline.Tooling.LocalizationXliff.csproj`. Update the `<ProjectReference>` to it in shipyard root `Directory.Build.props`.

**Gate per cluster:** Cluster build + tests green.

### Phase 4.7 — Consumer-side rewire — sunfish/ (1 PR in sunfish repo)

**Scope:** `sunfish/src/Sunfish.Anchor.csproj` + all 74 `namespace Sunfish.Anchor` files.
- Stays `Sunfish.Anchor.*` (preserved). But:
- Update all 27 `<ProjectReference Include="..\..\shipyard\packages\...\Sunfish.X.csproj" />` lines to new `Harborline.X.csproj` paths.
- Update all `using Sunfish.X;` in `src/**/*.cs` and `src/**/*.razor` → `using Harborline.X;` (except `using Sunfish.Anchor.*` which stays).

**Gate:** `dotnet build sunfish/Sunfish.slnx` green; Anchor MAUI workload build green on at least one platform.

### Phase 4.8 — Consumer-side rewire — signal-bridge/ (1 PR in signal-bridge repo)

**Scope:** All 10 signal-bridge csproj files (file name `Sunfish.Bridge.*.csproj` stays — Bridge is a product), but their ProjectReferences into shipyard need updating; all 130 `namespace Sunfish.Bridge` files keep their namespace, but their `using Sunfish.X;` lines update to `using Harborline.X;`.

**Gate:** `dotnet build signal-bridge/SignalBridge.slnx` green.

### Phase 4.9 — Doc + ICM + ADR sweep (1 PR in shipyard)

**Scope:** Remaining `.md` references to `Sunfish.Foundation.*` / `Sunfish.Kernel.*` / etc. in `shipyard/docs/`, `shipyard/icm/`, `shipyard/_shared/`. Whitelist excludes:
- `shipyard/icm/_archive/**` (frozen historical content)
- `the-inverted-stack/**` (book content, separate workstream)
- Any file containing the literal word "deprecated" + `Sunfish.X` (historical reference, leave as-is).

**Gate:** Documentation builds (mkdocs / docs site) green; spot-check 5 ADRs read sensibly.

### Phase 4.10 — Final verification + cleanup (1 PR; small)

**Scope:**
- Run `grep -r "namespace Sunfish\." shipyard/packages` — must return only whitelisted preservation set.
- Update `shipyard/CHANGELOG.md` with rename announcement.
- Update `MIGRATION.md` Phase 3 row to "EXECUTED — <date>".
- Author admiral-broadcast announcing completion.

**Gate:** Closing gate. PASS = ready to ratify Phase 3 closure.

---

### Mapping rules summary (§M)

| Today | Phase 3 outcome | csproj filename | Notes |
|---|---|---|---|
| `Sunfish.Foundation.*` | `Harborline.Foundation.*` | `Harborline.Foundation.csproj` etc. | 32 packages |
| `Sunfish.Kernel.*` | `Harborline.Kernel.*` | `Harborline.Kernel.csproj` etc. | 13 packages |
| `Sunfish.Blocks.*` | `Harborline.Blocks.*` | `Harborline.Blocks.X.csproj` | ~30 packages |
| `Sunfish.UIAdapters.*`, `Sunfish.UICore` | `Harborline.UIAdapters.*`, `Harborline.UICore` | matching csproj rename | 5 packages |
| `Sunfish.Compat.*` | `Harborline.Compat.*` | matching | 13 packages |
| `Sunfish.Federation.*` | `Harborline.Federation.*` | matching | 4 packages |
| `Sunfish.Ingestion.*` | `Harborline.Ingestion.*` | matching | 8 packages |
| `Sunfish.Providers.*` | `Harborline.Providers.*` | matching | 3 packages + helpers |
| `Sunfish.Analyzers.*` | `Harborline.Analyzers.*` | matching | 4 analyzers |
| `Sunfish.Wayfinder.Analyzers` | `Harborline.Wayfinder.Analyzers` | matching | 1 package |
| `Sunfish.Icons.*` | `Harborline.Icons.*` | matching | 2 packages |
| `Sunfish.Components.*` (if present) | `Harborline.Components.*` | matching | as found |
| `Sunfish.Tooling.*` | `Harborline.Tooling.*` | matching | 5 tooling projects |
| **§M-PRESERVE:** `Sunfish.Anchor.*` | **STAYS `Sunfish.Anchor.*`** | csproj filename **stays** `Sunfish.Anchor.csproj` | ERP product name |
| **§M-PRESERVE:** `Sunfish.Bridge.*` | **STAYS `Sunfish.Bridge.*`** (pending W#82) | csproj filename **stays** `Sunfish.Bridge.*.csproj` | Mesh product name; reassess in W#82 |

---

## 5. Verification

### Automated (CI)
- Per-cluster PR: `dotnet build shipyard/Shipyard.slnx` + `dotnet test` against cluster-scoped test projects.
- Cross-repo nightly: `dotnet build` on each of shipyard, sunfish, signal-bridge wired up via GitHub Actions matrix using sibling-checkout (already wired post-Phase-2).
- Sweep verification script: `grep -r "namespace Sunfish\." shipyard/packages | grep -v -f phase3-preserve.txt` — must return empty.

### Manual (review)
- Each cluster PR reviewed by Engineer + Admiral. csproj diffs read first (highest-signal). .cs diffs spot-checked.
- ADR sweep PR reviewed by FED + Yeoman.

### Ongoing observability
- After Phase 4.10 closure: weekly grep audit (admiral-loop check) confirms no `namespace Sunfish.[^A]` leaked back into shipyard packages.
- NuGet feed: confirm next package publish carries `Harborline.*` PackageIds.

---

## 6. Rollback Strategy

**Per-package revert (preferred granularity):**
1. Each cluster PR is independently revertable via `git revert <SHA>`.
2. Revert restores filenames, csproj content, .cs source, ProjectReferences in dependent csprojs (because Option-A PR shape includes them).
3. CI re-runs; expected green.
4. Mark cluster as "skipped" in plan tracking; resume next cluster.

**Mid-sweep abort:**
- If 3+ cluster PRs fail in a row → STOP sweep; reconvene with CIC.
- Partial rename is an acceptable end-state (the sweep is opt-in per cluster). The `phase3-preserve.txt` whitelist absorbs reverted clusters by simply re-adding their namespace prefix.

**Catastrophic state recovery:**
- Phase 2 closure left clean state at `RATIFICATION-2026-05-17.md`. Worst case: hard reset to the pre-Phase-3 commit on each repo's `main`, force-push (only with explicit CIC approval). Solo-dev posture per `MIGRATION.md` makes this safe — no other contributors lose work.

---

## 7. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Quiescence breaks mid-sweep (new branch lands on `main` that touches namespace surface) | Medium | High | Lock `main` after Phase 4.0 lands; require admiral approval for any PR touching csproj or namespace until Phase 4.10. |
| `Directory.Build.props` predicate update missed → analyzer auto-wiring silently disabled → CS errors stop surfacing | Medium | High | Phase 4.1 PR explicitly includes the predicate update; reviewer checklist item. |
| `InternalsVisibleTo` entries missed → test projects can't see internals → test failures | Medium | Medium | Tooling script enumerates all `InternalsVisibleTo` entries cluster-wide; reviewer spot-checks. |
| Source generator (any) emits `Sunfish.*` namespace at compile time → namespace leaks back in | Low | Medium | Phase 4.10 grep audit catches it; source generators (analyzers cluster, design-tokens-codegen) audited in Phase 4.6. |
| Cross-repo consumer (sunfish, signal-bridge) gets out of sync with shipyard PR cadence → red builds | Medium | Low (per repo) | Schedule Phase 4.7 and 4.8 within 24 hours of all shipyard clusters merging. |
| The `Authors=Sunfish Contributors` / `Company=Sunfish` properties in `Directory.Build.props` get changed by accident → Phase 4 (marketing) collides | Low | Low | Explicit out-of-scope statement in tooling; reviewer enforces. |
| MAUI / Anchor preview workload regression unrelated to rename surfaces as a false positive | Medium | Low | Anchor's MAUI build is already flaky on preview workloads (per RATIFICATION watch-item #7); treat as pre-existing if symptoms match. |
| ADR / ICM doc sweep updates content inside `_archive/` accidentally | Medium | Low | Tooling whitelist excludes `_archive/` paths. |

---

## 8. Dependencies & Blockers

### Must merge before Phase 3 starts (quiescence)
- **cohort-1** (FED cohort PRs — currently at PR3 / PR4)
- **W#68** (Engineer — blocks-financial-payments, recently directed)
- **W#69, W#70** (status TBD per QM round-5 snapshot)
- **W#71** (blocks-docs attachment substrate — Engineer queue per 2026-05-18T02:40Z amendment)
- **W#23 P6** (po-mac iOS home screen)
- **WS-E** (Engineer — outbound messaging substrate, hand-off complete 2026-05-18)

### Must be ratified before Phase 3 starts
- **A1 mapping** — ✅ CIC ratified 2026-05-18T03:30Z per book rebrand mapping ruling.
- **W#82 Bridge namespace fate** — if `Sunfish.Bridge.*` is to rename in this phase (rather than preserve), the §M-PRESERVE table inverts and signal-bridge PR is much larger. Open W#82 with PAO/Admiral before kickoff.

### Tooling dependencies
- Phase 4.0 deliverable (`tooling/phase3-rename/`) must exist before any other phase.

---

## 9. Resume Protocol

A fresh agent landing on this plan mid-execution should:
1. Read this file end-to-end. Check Phase 4.X status by inspecting `coordination/inbox/admiral-status-*phase-3-*` records.
2. Run `grep -r "namespace Sunfish\." shipyard/packages | wc -l` — compare to baseline 3,306. Each phase reduces the count.
3. Check `shipyard/tooling/phase3-rename/` exists and the script runs in dry-run mode.
4. Read `shipyard/CHANGELOG.md` to see which clusters are listed as renamed.
5. Resume at the next cluster in §4 ordering, or HALT and ping CIC if a FAILED condition (§2) is active.

---

## 10. Timeline & Deadlines

- **D+0 (after A3 quiescence):** Phase 4.0 — tooling preparation (1 day).
- **D+1 to D+2:** Phase 4.1 — foundation cluster (1 day exec + 1 day review).
- **D+3:** Phase 4.2 — kernel cluster.
- **D+4 to D+5:** Phase 4.3 + 4.4 — UI + compat.
- **D+6 to D+8:** Phase 4.5 — blocks cluster (largest).
- **D+9 to D+11:** Phase 4.6 — federation/ingestion/providers/analyzers/tooling (5 small PRs in parallel-ish).
- **D+12 to D+13:** Phase 4.7 + 4.8 — consumer-repo rewires.
- **D+14:** Phase 4.9 + 4.10 — docs + finalization.

Timeout (per §2 FAILED F5): D+14. Slip permitted at cluster boundaries. Hard stop at D+21 → CIC reconvene.

---

## 11. Reference Library

- `/Users/christopherwood/Projects/Harborline-Software/RATIFICATION-2026-05-17.md` — Phase 2 closure
- `/Users/christopherwood/Projects/Harborline-Software/MIGRATION.md` — runbook, Phase 3 row
- `/Users/christopherwood/Projects/Harborline-Software/.claude/rules/universal-planning.md` — UPF v1.2
- `/Users/christopherwood/Projects/Harborline-Software/shipyard/Directory.Build.props` — analyzer auto-wiring predicates (must update in Phase 4.1)
- `/Users/christopherwood/Projects/Harborline-Software/shipyard/Directory.Build.targets` — XLIFF tooling Import (touch in Phase 4.6 tooling cluster)
- `/Users/christopherwood/Projects/Harborline-Software/shipyard/BannedSymbols.txt` — orthogonal; do not touch
- `/Users/christopherwood/Projects/Harborline-Software/shipyard/Shipyard.slnx` — cluster entries; touch each phase
- `/Users/christopherwood/Projects/Harborline-Software/sunfish/src/Sunfish.Anchor.csproj` — Phase 4.7 target
- `/Users/christopherwood/Projects/Harborline-Software/signal-bridge/Sunfish.Bridge/Sunfish.Bridge.csproj` — Phase 4.8 target
- `/Users/christopherwood/Projects/Harborline-Software/coordination/inbox/admiral-ruling-2026-05-18T03-30Z-pao-vol1-vol2-rebrand-mapping-cic-locked.md` — CIC A1 ratification

---

## 12. Cold Start Test

A fresh agent reading only this file + the Reference Library should be able to:
- Identify that A3 quiescence is the first blocker now that A1 is ratified (§3, §8).
- Locate the tooling stub (§4.0) before touching any source.
- Run the cluster ordering (§4.1 → §4.10).
- Know to STOP on any FAILED condition (§2).
- Find rollback procedure (§6) on any cluster-PR failure.

Cold-start verified by Admiral self-review on draft. Requires Engineer + Yeoman second-pair review before kickoff.

---

— End of Phase 3 Namespace Rename Migration Plan —
