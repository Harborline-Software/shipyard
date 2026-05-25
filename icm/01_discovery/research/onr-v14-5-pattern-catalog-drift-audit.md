# Pattern catalog drift audit — post-cohort-3 + post-#142 (V14 #5)

**Authored by:** ONR (V14 batch item #5)
**Requester:** Admiral (per V14 standing-dispatch directive 2026-05-25)
**Authored at:** 2026-05-25T1930Z
**Status:** draft
**Supersedes (informational):** V7 #6 catalog snapshot
(`shipyard/_shared/engineering/pattern-catalog-2026-05-22-snapshot.md`) is now 3 days
stale. This V14 audit refreshes the prior snapshot with post-#132 (formal pattern-014
ratification) and post-#142 (cohort-4 Stage-05 retro fold) evidence.

---

## Scope

The pattern catalog (`shipyard/_shared/engineering/standing-approved-patterns.md`) has
grown to 11 formal ratified patterns + 5+ candidates referenced in PRs / verdicts / cohort-3
design docs. The catalog is the trust-amortization mechanism for the standing pattern
short-pipeline; drift between catalog state and PR claims directly costs throughput
(council load + duplicate review).

This audit asks four questions:

1. **Retire?** Are there patterns that should be retired (obsolete or superseded)?
2. **Consolidate?** Are there patterns that overlap and should consolidate?
3. **Split?** Are there patterns that should be split (per pattern-012a/b precedent)?
4. **Promote-to-default?** Are there "always-on" patterns that should be promoted to
   substrate-default (e.g., commitlint pre-flight; tenant-scoping)?

**Out of scope:** ratification of individual recommended actions (Admiral / CIC call);
catalog-edit PR authoring (that is a separate hygiene PR if Admiral accepts findings).

---

## TL;DR

1. **0 patterns recommend retirement.** All 11 formal patterns are actively shipping.
   pattern-007 (ledger-flip) has the smallest active-shipping surface but still ships
   ~weekly; not stale enough to retire.

2. **2 consolidation opportunities identified, both LOW priority.**
   - pattern-006 (apps/docs overview) + pattern-008 (docs page touch-up) could consolidate
     to a single docs-pattern with sub-criteria. They have different matching criteria
     (new doc file vs existing doc edit) but identical council-requirement (SKIP); each
     has its own shipping history. Consolidation saves catalog reading time but loses
     diagnostic granularity. **Recommend leave as-is.**
   - pattern-009-tenant-keying-retrofit could be absorbed into pattern-009 as a sub-pattern
     once the retrofit pass is complete (no more retrofit instances anticipated).
     **Recommend leave as-is until cohort-N where no retrofit candidates remain;** the
     sub-pattern's distinct council requirement (dual SPOT-CHECK + Admiral attest precedent)
     is still load-bearing.

3. **2 split opportunities forward-watched, neither requiring immediate action.**
   - pattern-009 itself: cohort-1 instances (Properties / Leases / Maintenance) vs cohort-2
     instances (Financial cluster) differ in CSRF posture (cookie-auth vs Field route family).
     The catalog acknowledges this in the "CSRF requirement" sub-section. Not a true split;
     more like a sub-criterion clarification. **No action needed.**
   - pattern-015 (provisional report surface) Meaning A vs Meaning B split (see V14 #1):
     forward-watch only; do not pre-split.

4. **1 promote-to-default candidate identified: substrate-default tenant-scoping.**
   Per the cohort-1 + cohort-2 + cohort-4 instances of pattern-009 and pattern-009-tenant-
   keying-retrofit, EVERY Bridge endpoint must perform server-derived tenant scoping. This
   is no longer pattern-specific; it's substrate-default. **Recommend codify as a substrate
   convention** in `fleet-conventions.md` or a new `_shared/engineering/substrate-defaults.md`,
   moving the tenant-scoping check from per-pattern requirement to baseline expectation.

5. **3 drift findings persist from V7 #6 snapshot:**
   - **Drift A — pattern-004 duplicated entry (formal AND candidate sections):** V7 #6
     flagged this; PR #132 fixed it per the commit message claim. ONR verifies fix is
     applied — formal entry at lines 148-173 is canonical; candidate-section duplicate is
     removed. **Drift A CLOSED.**
   - **Drift B — pattern-013-cartridge-read-via-post and pattern-012a/b still candidate:**
     pattern-013 and pattern-012a/b are now in the catalog as candidates per PR #132. No
     drift on entry status. **Drift B CLOSED (entry exists; ratification pending shipping).**
   - **Drift C — pattern-015 / 016 / 017 NOT in main catalog:** the cohort-3 design docs
     define them but the catalog has only candidates 010 / 011 / 012a / 012b / 013. The
     catalog file lacks entries for 015 / 016 / 017. **DRIFT D-NEW (post-V7 #6):** PR-
     authoring discipline gap — cohort-3 design direction PR (#116 MERGED) did NOT also
     extend the catalog. Pattern catalog should mirror the cohort-3 candidate-pattern docs
     so the canonical catalog is the single source of truth.

6. **2 new drift findings surfaced by this audit:**
   - **DRIFT D-NEW:** pattern-015 / 016 / 017 not in catalog (above).
   - **DRIFT E-NEW:** The V7 #6 snapshot recommended adding a "Last audited" date stamp
     to the catalog. PR #132 did not include this. **Recommend add `last-audited:
     2026-05-25` to the catalog frontmatter** in the next catalog-edit PR.

---

## 1. Catalog state — current (post-#132 + post-#142)

### 1.1 Formal patterns (11)

| # | Pattern | Last shipped | Active instances tracked |
|---|---|---|---|
| 1 | `pattern-001` Cluster scaffold + Repository + DI | ongoing (~weekly) | 5+ (many in pre-2026-05-17 history) |
| 2 | `pattern-002` ERPNext importer Pass 1 | ongoing | 5+ |
| 3 | `pattern-003` Cluster posting service | ongoing | 3 (canonical) |
| 4 | `pattern-004` Cluster aging service | 2 instances; ratified per PR #132 | 2 (waiting for third — cohort-5 AP Aging is the trigger; see V14 #2) |
| 5 | `pattern-005` DI extension `Add<Block>()` | ongoing | many |
| 6 | `pattern-006` apps/docs/blocks overview | ongoing | many |
| 7 | `pattern-007` Ledger row flip | ongoing | 10+ |
| 8 | `pattern-008` Docs page touch-up | ongoing | 5+ |
| 9 | `pattern-009` Bridge endpoint + frontend rebind pair | ongoing (cohort-1 + 2 + 4) | 6+ |
| 10 | `pattern-009-tenant-keying-retrofit` Tenant-keying retrofit pass | cohort-2 PR 0a/b/c/d | 4 (ratification basis) |
| 11 | `pattern-014` Bridge cross-tenant audit emission | cohort-4 + signal-bridge 29/31/33 | 4 (ratification basis per PR #132) |

### 1.2 Candidate patterns in catalog (5)

| # | Pattern | Shipped instances | Ratification gate |
|---|---|---|---|
| 10 | `pattern-010` apps/docs toc.yml entry | bundled with pattern-006 | unclear (low priority) |
| 11 | `pattern-011` Cross-cluster event publisher | 0 | 3 shipping instances |
| 12a | `pattern-012a-tenant-scoped-write-path` | 2 (cohort-2 PR 3 + signal-bridge#29) | 1 more shipping instance |
| 12b | `pattern-012b-accountant-grade-write-path` | 2 (signal-bridge#36 + #37) | 1 more shipping instance |
| 13 | `pattern-013-cartridge-read-via-post` | 0 (will ship on cohort-3 first-PR) | 3 shipping instances |

### 1.3 Candidate patterns NOT in catalog (3)

| # | Pattern | Source | Status |
|---|---|---|---|
| 15 | `pattern-015-provisional-report-surface` | `_shared/design/cohort-3/provisionality-banner-pattern.md` | Defined; not in canonical catalog (DRIFT D) |
| 16 | `pattern-016-run-on-demand-report` | `_shared/design/cohort-3/run-on-demand-pattern.md` | Defined; not in canonical catalog (DRIFT D) |
| 17 | `pattern-017-csv-export-affordance` | `_shared/design/cohort-3/csv-export-pattern.md` | Defined; not in canonical catalog (DRIFT D) |

### 1.4 Reserved slots without definition (1)

- pattern-018 — next available slot for any new candidate

---

## 2. Retirement analysis

A pattern is a candidate for retirement when ALL of these hold:

- **Obsolete:** the architectural shape it codifies is no longer recommended for new work
- **Superseded:** another pattern covers its space more comprehensively
- **No active shipping:** zero instances in the trailing 30-day window

**Audit result:** 0 patterns meet retirement criteria.

The closest call is `pattern-007` (ledger row flip — single-row updates to
`active-workstreams.md`). With the QM daemon now handling many ledger-flip operations
automatically, the human-PR ledger-flip surface has shrunk. But pattern-007 still ships
weekly (~10+ active instances over the past month). Not stale enough to retire.

**Recommend:** no retirements.

---

## 3. Consolidation analysis

A consolidation opportunity exists when two patterns share enough criteria that combining
them simplifies the catalog without losing diagnostic value.

### 3.1 pattern-006 + pattern-008 (docs patterns)

- pattern-006: new `apps/docs/blocks/<cluster>/overview.md` file
- pattern-008: edits to existing `apps/docs/**.md` files

Both: SKIP council requirement; no test changes; >5 shippings each; same revoke criteria.

**Consolidation option:** single `pattern-docs` with sub-criteria (new file vs existing
file).

**Trade-off:**
- Pro: smaller catalog; one less pattern ID to remember
- Con: harder to claim mechanically (`@standing-pattern: pattern-006` is more diagnostic
  than `@standing-pattern: pattern-docs`); also loses the "are we shipping new docs vs
  editing existing" signal at the catalog level

**Recommend:** **LEAVE AS-IS.** The diagnostic granularity is small but real; catalog size
is not a binding constraint.

### 3.2 pattern-009-tenant-keying-retrofit absorbing into pattern-009

The retrofit sub-pattern was ratified specifically because cohort-2 PR 0a/0b/0c/0d
exercised the tenant-keying cross-check on pre-existing handlers (not new endpoints). With
the retrofit pass complete, future instances would be `pattern-009` (new endpoint pairs)
not `pattern-009-tenant-keying-retrofit` (retrofit pass).

**Absorption option:** mark `pattern-009-tenant-keying-retrofit` as "ratified;
no-new-instances" status and treat the tenant-keying cross-check as a baseline criterion
of pattern-009 going forward.

**Trade-off:**
- Pro: simplifies pattern-009 catalog entry; reflects that retrofit pass is complete
- Con: pattern-009-tenant-keying-retrofit's specific dual SPOT-CHECK requirement (per
  cohort-2 PR 0d Admiral attest) is documented evidence; absorbing it loses some of that
  precedent

**Recommend:** **LEAVE AS-IS** but consider absorption in a future catalog refresh (e.g.,
6 months from now) when the retrofit pass is unambiguously closed and no further retrofit
candidates are forecast. The current catalog entry costs ~30 lines; not a binding cost.

---

## 4. Split analysis

A split opportunity exists when two distinct architectural shapes are emerging under the
same pattern ID, AND the shapes' negative-criteria boundaries differ materially.

### 4.1 pattern-009 — Cohort-1 vs Cohort-2 CSRF posture

The catalog acknowledges this in the "CSRF requirement" section: cookie-auth route family
requires CSRF; Field route family uses pairing-token / mTLS. This is a sub-criterion
clarification, not a true split.

**Recommend:** **NO ACTION.** The sub-criterion handling is adequate.

### 4.2 pattern-015 — Meaning A vs Meaning B (provisionality)

Per V14 #1 — forward-watch only; do not pre-split. The Meaning B (accumulator-not-yet-
flushed) variant has not yet shipped (would emerge on cohort-5 ARR/MRR per V5 #1a). If
cohort-5 ARR/MRR ships and surfaces Meaning B, file an `admiral-ruling-*-pattern-015-
split` for 015a/015b at that point.

**Recommend:** **FORWARD-WATCH ONLY** for cohort-5+ ARR/MRR ship.

### 4.3 pattern-012a/b split — Already done

PR #132 split pattern-012 into 012a (tenant-scoped write-path) and 012b (accountant-grade
write-path) per admiral-ruling-2026-05-22T18-25Z. The split is in the catalog; no
additional action needed.

**Recommend:** **CLOSED.**

---

## 5. Promote-to-default analysis

A "promote-to-default" opportunity exists when a pattern's claim shifts from "additive
discipline" to "baseline expectation" — every PR exhibits it, so claiming it via
`@standing-pattern:` is no longer signal.

### 5.1 Substrate-default tenant-scoping (PROPOSED PROMOTION)

**Evidence:**
- pattern-009 requires tenant cross-check sub-step on state-mutating handlers
- pattern-009-tenant-keying-retrofit's whole purpose was to bring legacy endpoints into
  conformance
- pattern-014 (cross-tenant audit emission) is the audit-emission baseline for tenant
  cross-checks
- ADR 0091 (ITenantContext) and ADR 0092 (server-derived tenant) codify the substrate-
  level requirement
- Cohort-4 cycle 0 RED verdict's A1-1 finding ("client-side defense-in-depth is dead code
  because the server is the boundary; the substrate is the canonical layer") explicitly
  notes the server is the security boundary per ADR 0092

**Argument for promotion:** every Bridge endpoint must perform server-derived tenant
scoping. This is no longer pattern-specific; it's substrate-default. Pattern-009 still
applies for the wire-pairing aspect, but the tenant-scoping aspect is baseline.

**Proposed promotion path:**

1. Add a new section to `fleet-conventions.md` under "Critical conventions every agent
   must respect": **Substrate-default: server-derived tenant scoping.** A one-paragraph
   pin naming the requirement, citing ADR 0091 + 0092, and listing the consequences for
   non-conforming code.

2. Update pattern-009 catalog entry to reference the substrate-default and trim the
   tenant cross-check sub-step from pattern-009 (or pin it as "inherited from substrate
   default; documented here for cross-reference").

3. Update pattern-009-tenant-keying-retrofit to status "ratified; retrofit pass complete;
   no-new-instances expected" since all legacy endpoints have been retrofitted.

**Cost:** ~30 minutes of catalog-edit time + ~10 minutes of fleet-conventions edit time.

**Benefit:** future PRs don't need to claim tenant-scoping as a pattern requirement; it's
baseline. Council SPOT-CHECK load drops slightly (cross-check is verified once at the
substrate level, not per-PR).

**Recommend:** **RATIFY.** Substrate-default tenant-scoping promotion is a clear win.

### 5.2 Commitlint pre-flight discipline (FORWARD-WATCH)

**Evidence:**
- Multiple memory notes (`feedback_fleet_commitlint_gotchas`, `feedback_commitlint_W_NN_
  shorthand_body_trap`) document commitlint footer traps
- Pre-flight `git log -1 --format=%B | grep -E '[A-Za-z]#[0-9]'` is the recommended check
- This is currently a memory note / convention, not a pattern

**Argument for codification:** every PR author should run the pre-flight check; missing
it costs commit revisions.

**Counter-argument:** this is operational hygiene, not a pattern. The commit-message
shape is captured in commitlint configuration; no architectural pattern is appropriate.

**Recommend:** **NO PROMOTION.** This stays as a memory note / fleet-conventions section
under "Commit-message commitlint traps." The fleet-preflight hook (per the post-V14 #2
commit canary) is the automation surface; pattern-claim is wrong scope.

### 5.3 SPOT-CHECK dispatch SLA (NO PROMOTION)

The 30-min SPOT-CHECK dispatch SLA is already codified in `fleet-conventions.md`. Not a
pattern. No action needed.

---

## 6. New drift findings (post-V7 #6)

### 6.1 DRIFT D-NEW: pattern-015 / 016 / 017 not in canonical catalog

**Discovery:** the cohort-3 candidate patterns (provisionality banner / run-on-demand /
CSV export) are defined in `_shared/design/cohort-3/*.md` as `pattern-015 / 016 / 017`
but the canonical pattern catalog (`_shared/engineering/standing-approved-patterns.md`)
does not have entries for these slots. The catalog has candidate entries for
010 / 011 / 012a / 012b / 013 but skips 014 (which is formal as of #132) and 015 / 016 /
017 (which are candidates in design docs).

**Root cause:** PR #116 (cohort-3 design direction MERGED 2026-05-22) introduced the
candidate-pattern docs but did NOT extend the canonical catalog. The cohort-3 docs are
the source of truth for the pattern bodies; the catalog should mirror them.

**Resolution:** small catalog-edit PR adding three candidate entries with appropriate
cross-references to the cohort-3 design docs. ~30 minutes of authoring.

**Recommend:** **FILE CATALOG-EDIT PR** to close DRIFT D.

### 6.2 DRIFT E-NEW: "Last audited" date stamp missing

**Discovery:** V7 #6 snapshot recommended adding a `last-audited: YYYY-MM-DD` stamp to
the catalog frontmatter. PR #132 (which addressed multiple V7 #6 findings) did NOT add
this stamp.

**Root cause:** PR #132's scope was pattern-014 ratification + pattern-012a/b adds +
pattern-013 add + pattern-004 dedup. The date-stamp recommendation was not in scope.

**Resolution:** trivial addition; include in the same catalog-edit PR that closes
DRIFT D.

**Recommend:** **FILE CATALOG-EDIT PR** to close DRIFT E (bundle with DRIFT D).

---

## 7. Always-on patterns (substrate-default candidates)

Per §5, the only clear always-on promotion is server-derived tenant scoping. Other
always-on candidates surveyed:

| Candidate | Always-on? | Promote? |
|---|---|---|
| Server-derived tenant scoping | YES (every Bridge endpoint) | **YES — promote** |
| `@candidate-pattern:` / `@standing-pattern:` PR-body discipline | YES (every PR that claims a pattern) | NO — already convention |
| Commitlint pre-flight | YES (every commit) | NO — operational hygiene, not architectural |
| Worktree-in-tree (`.worktrees/`) | YES (every worktree) | NO — already in fleet-conventions |
| `core.filemode false` on Windows | YES (every Windows clone) | NO — already in fleet-conventions |
| SPOT-CHECK 30-min SLA | YES (every standing-pattern PR open) | NO — already in fleet-conventions |
| Cleanest-long-term option | YES (every implementation choice) | NO — already standing directive |
| Engineer PR-count cap (10) | YES (Engineer session) | NO — already in fleet-conventions |

Net: server-derived tenant scoping is the only architectural always-on candidate worth
promoting. Other always-ons are operational/process and already codified.

---

## 8. Catalog-edit PR proposal

ONR proposes a single catalog-edit PR to close the surfaced drift + promotion. Scope:

1. Add `last-audited: 2026-05-25` to catalog frontmatter (DRIFT E close)
2. Add pattern-015 / 016 / 017 candidate entries with cross-references to cohort-3
   design docs (DRIFT D close)
3. Add a new section "Substrate-default conventions (promoted from patterns)" naming
   server-derived tenant scoping with citation to ADR 0091 + 0092
4. Update pattern-009 catalog entry to reference the substrate-default for tenant scoping
   (cross-reference, not removal)
5. Update pattern-009-tenant-keying-retrofit to mark "ratified; retrofit pass complete;
   no-new-instances expected" status (informational)

**Effort:** ~45 minutes ONR authoring; pattern-006 or pattern-008 (docs patterns)
applies; no council SPOT-CHECK required (pure catalog hygiene).

**Recommend:** **AUTHOR ON ADMIRAL DISPATCH** as a V15+ batch item OR as a follow-on
from this audit.

---

## 9. Open questions for Admiral

1. **Catalog-edit PR authoring.** Should ONR author the proposed catalog-edit PR now
   (within V14 batch) as a 6th deliverable, or wait for Admiral dispatch in a future
   batch? ONR recommends author now if the V14 budget allows; the edits are mechanical
   and small.

2. **Substrate-default tenant-scoping promotion.** ONR recommends RATIFY. Confirm Admiral
   agrees the promotion is appropriate? If yes, ONR includes the new section in the
   catalog-edit PR.

3. **pattern-009-tenant-keying-retrofit status update.** ONR recommends marking "ratified;
   no-new-instances expected" — confirm Admiral agrees the retrofit pass is complete
   (no further legacy endpoints are scheduled for retrofitting).

4. **pattern-015/016/017 catalog entry authoring authority.** Should ONR author the
   catalog entries directly (mirroring the cohort-3 design doc text), or wait for the
   cohort-3 first-instance ship + PAO design-direction sign-off? ONR recommends author
   now; the design docs are the canonical source.

---

## 10. Sources cited

1. `shipyard/_shared/engineering/standing-approved-patterns.md` (canonical catalog;
   current state post-#132)
2. `shipyard/_shared/engineering/pattern-catalog-2026-05-22-snapshot.md` (V7 #6 prior
   snapshot; the audit this refresh supersedes)
3. PR #132 (governance: pattern-014 ratification + pattern-012a/b + pattern-013 candidate
   adds + pattern-004 fix; MERGED 2026-05-22T13:15Z)
4. PR #142 (cohort-4 Stage-05 first-pilot retro + ADR 0093 Rev 4 fold; MERGED
   2026-05-25T15:25Z)
5. PR #103 (pattern-009-tenant-keying-retrofit promotion; MERGED 2026-05-22T07:26Z)
6. `shipyard/_shared/design/cohort-3/provisionality-banner-pattern.md` (pattern-015
   canonical body; DRIFT D source)
7. `shipyard/_shared/design/cohort-3/run-on-demand-pattern.md` (pattern-016 canonical body)
8. `shipyard/_shared/design/cohort-3/csv-export-pattern.md` (pattern-017 canonical body)
9. `shipyard/docs/adrs/0091-itenantcontext.md` + `0092-server-derived-tenant.md`
   (substrate-default tenant-scoping ADR basis)
10. `coordination/inbox/admiral-ruling-2026-05-22T18-25Z-pattern-012-split-into-012a-012b-supersedes-17-35Z.md` (pattern-012a/b split precedent)
11. `coordination/inbox/admiral-attest-2026-05-21T01-10Z-shipyard-64-pr-0d-dual-green-promote.md` (pattern-009-tenant-keying-retrofit ratification basis)
12. `shipyard/.claude/rules/fleet-conventions.md` (target for substrate-default promotion)

---

## 11. What ONR does next

V14 #5 deliverable complete. Files `onr-status-2026-05-25T1930Z-v14-5-pattern-catalog-
drift-audit-complete.md`. V14 batch CLEARED (5/5 PRs filed).

If Admiral approves the catalog-edit PR proposal (§8) within current batch, ONR proceeds
to author as V14 #6. Otherwise, ONR stands by for V15 dispatch.

— ONR, 2026-05-25T19:30Z
