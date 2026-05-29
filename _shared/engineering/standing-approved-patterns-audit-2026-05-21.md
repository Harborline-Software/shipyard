# Standing-approved patterns catalog audit (2026-05-21)

**Authored by:** ONR (V4 batch item #4)
**Requester:** Admiral (per `admiral-directive-2026-05-21T14-05Z` item #4)
**Authored at:** 2026-05-21T14-18Z
**Status:** research / proposal (catalog edits pending Admiral routing per ADR-authoring scope precedent set in `admiral-ruling-2026-05-21T14-10Z`)

---

## Scope

Audit `shipyard/_shared/engineering/standing-approved-patterns.md` for:
1. Pattern number contiguity (1-N inventory; gaps)
2. Candidate vs formal status accuracy
3. Forward-watch entries currency
4. Cross-reference of `@standing-pattern:` / `@candidate-pattern:` claims against catalog
5. Orphan pattern numbers (referenced in code/PRs but not in catalog) OR catalog entries with no claims

Deliverable: this audit doc + recommended cleanup PR scope.

---

## Inventory — current catalog state (2026-05-21T14:18Z)

### Formal (ratified) patterns — main catalog (line 50-318)

| # | Title | Status | Recent activity |
|---|---|---|---|
| pattern-001 | Cluster scaffold + Repository + DI | RATIFIED FORMAL | stable |
| pattern-002 | ERPNext importer Pass 1 | RATIFIED FORMAL | stable |
| pattern-003 | Cluster posting service | RATIFIED FORMAL | stable |
| pattern-004 | Cluster aging service | RATIFIED FORMAL — but ALSO listed as "candidate; needs 1 more shipping" at line 322 — INCONSISTENT | flag |
| pattern-005 | DI extension `Add<Block>()` umbrella | RATIFIED FORMAL | stable |
| pattern-006 | `apps/docs/blocks/<cluster>/overview.md` authoring | RATIFIED FORMAL | stable |
| pattern-007 | Ledger row flip | RATIFIED FORMAL | stable |
| pattern-008 | Docs page touch-up | RATIFIED FORMAL | stable |
| pattern-009 | Bridge endpoint + companion frontend binding pair | RATIFIED FORMAL | active (cohort-1/2/3) |

### Candidates / proposed (line 320-325)

| # | Title | Status | Activity |
|---|---|---|---|
| pattern-004 | Cluster aging service | "needs 1 more shipping to reach 3-PR ratification minimum" — INCONSISTENT with main catalog listing as FORMAL | **GAP — needs resolution** |
| pattern-009-tenant-keying-retrofit | Tenant-keying retrofit sub-pattern | **RATIFIED FORMAL 2026-05-21** per cohort-2 PR 0a-d clean shipping (shipyard#52/57/60/64) + dual sec-eng + .NET-architect SPOT-CHECK + shipyard#63 corrigendum. **CATALOG NOT UPDATED.** | **STALE — needs catalog promotion** |
| pattern-010 | apps/docs/blocks/<cluster>/toc.yml entry | "usually bundled with pattern-006; not a standalone pattern yet" | stable; status unchanged |
| pattern-011 | Cross-cluster event publisher wiring | needs 3 shippings | stable; status unchanged |

### NEW (added today via V3 #2 / shipyard#77)

| # | Title | Status | Activity |
|---|---|---|---|
| pattern-012-financial-write-path | Financial Bridge POST + CSRF + audit + cross-tenant rejection | candidate; 1st instance shipped (cohort-2 PR 3 RentCollection POST); 3rd-instance ratification candidate W#60 P4 PR 2 JournalEntry POST | active per V3 #2 |

---

## Gaps + inconsistencies surfaced

### Gap 1 — Pattern-004 is BOTH formal (line 148-) AND candidate (line 322)

**Inconsistency:** the catalog has pattern-004 listed as a formal entry in the main catalog (§Catalog) AND as a candidate in §"Patterns proposed but not yet ratified."

**ONR's read:** pattern-004 was likely PROMOTED to formal at some point but the candidate-section entry wasn't removed. Catalog hygiene drift.

**Recommended cleanup:** REMOVE line 322 from §"Patterns proposed but not yet ratified" (the formal entry at line 148+ is canonical).

### Gap 2 — Pattern-009-tenant-keying-retrofit RATIFIED but not in main catalog

**Status:** ratification trigger met 2026-05-21 (4 clean substrate shippings + dual SPOT-CHECK + #63 corrigendum); Admiral acknowledged in `admiral-ruling-2026-05-20T19-30Z-engineer-cohort-2-pr-0a-0b-0c-unified-green-attest.md` and the corrigendum at shipyard#63.

**Catalog state:** still listed in §"Patterns proposed but not yet ratified" (line 323).

**ONR's read:** ratification happened; catalog hasn't been updated. **CRITICAL hygiene gap.**

**Recommended cleanup:** PROMOTE pattern-009-tenant-keying-retrofit to formal entry in main catalog (between current pattern-009 and (proposed) pattern-010). Author the formal entry shape (mirror pattern-001..009 format). REMOVE the candidate-section entry.

### Gap 3 — Pattern-010 + pattern-011 candidate status currency

**Status:** both have been "candidate" since post-cohort-1 era. No movement.

**ONR's read:** pattern-010 (`apps/docs/blocks/<cluster>/toc.yml` entry — bundled with pattern-006) is unlikely to ever be a standalone pattern. **RECOMMEND consolidating into pattern-006's formal entry as a sub-pattern note** rather than carrying as a phantom candidate.

Pattern-011 (cross-cluster event publisher wiring) — original "candidate; renumbered from pattern-009"; still 0 shippings observed. **RECOMMEND demoting to a "deferred / TBD candidate" status with explicit dormancy note**, OR removing if no concrete usage emerges within 60 days.

### Gap 4 — Pattern-012-financial-write-path needs catalog ADD

**Status:** ONR added the entry to §"Patterns proposed but not yet ratified" in V3 #2 (shipyard#77 MERGED).

**Catalog state:** entry now present at the catalog's candidate section.

**ONR's read:** GOOD — V3 #2 work landed correctly.

**Recommended cleanup:** none for pattern-012 specifically; await 3rd-instance ratification (W#60 P4 PR 2 JournalEntry POST per V3 #2 recommendation).

### Gap 5 — Number contiguity

**Inventory:** 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 — fully contiguous post V3 #2.

**Plus sub-patterns:** pattern-009-tenant-keying-retrofit (slot under pattern-009).

**No gaps; no orphans.** Healthy.

### Gap 6 — Forward-watch entries currency

The catalog's §"What's explicitly NOT a standing pattern" list (line 326+) covers always-needs-full-pipeline surfaces. Per cerebrum + recent activity:

- `accelerators/anchor/` substrate (ADR 0088) — **STALE PATH** post-restructure; should be `sunfish/apps/desktop/src-tauri/` per the migration (sunfish is the new Anchor host)
- `accelerators/bridge/Sunfish.Bridge/Cockpit/**` — **STALE PATH**; should be `signal-bridge/Sunfish.Bridge/Cockpit/**`
- `accelerators/bridge/Sunfish.Bridge/Features/Identity/**` — same; `signal-bridge/Sunfish.Bridge/Features/Identity/**`
- `_shared/product/local-node-architecture-paper.md` — verify still at this path post-restructure (likely OK)

**ONR's read:** post-restructure path migration drift in the "NOT a standing pattern" list. Several path references still use the legacy `accelerators/` prefix.

**Recommended cleanup:** path normalization sweep against the post-restructure layout per `fleet-conventions` §"Post-2026-05-17 migration layout."

### Gap 7 — Catalog maintenance attribution drift

Line 343: "XO authority: XO proposes new patterns + revocations" + "CO authority: CO ratifies new patterns + can revoke at any time."

**Status:** post-restructure the roles are Admiral (formerly XO) and CIC (formerly CO). Per memory + agent definitions.

**Recommended cleanup:** replace "XO" → "Admiral" and "CO" → "CIC" in §"Catalog maintenance" + §"Initial rollout plan" subsections.

---

## Recommended cleanup PR scope

ONR proposes a single cleanup PR with the following surgical edits:

1. **Promote pattern-009-tenant-keying-retrofit to formal** — add formal-entry-shape between pattern-009 and the candidate section; remove candidate-section line 323 reference (50-75 lines added to main catalog)
2. **Remove pattern-004 candidate line 322** (1 line removed)
3. **Reconsider pattern-010 + pattern-011** — either consolidate into related patterns OR add explicit dormancy notes (2-5 lines)
4. **Path migration sweep** — `accelerators/anchor/` → `sunfish/apps/desktop/`; `accelerators/bridge/Sunfish.Bridge/` → `signal-bridge/Sunfish.Bridge/` (3-5 path edits in §"What's explicitly NOT a standing pattern")
5. **Role attribution sweep** — XO → Admiral; CO → CIC (3-5 instances in §"Catalog maintenance" + §"Initial rollout plan")
6. **Date stamp** — add "Last audited: 2026-05-21" near the top of the catalog OR in §"Catalog maintenance" subsection

**Total edit scope:** ~70-100 lines net change. Mechanical / hygiene; no architectural disposition changes.

**Pre-merge council:** advisory only (catalog hygiene; not security-relevant surface). Admiral self-attests via inbox status.

---

## Cross-reference — `@standing-pattern:` / `@candidate-pattern:` claims in recent PRs

Sample of recent PR commit messages + bodies (via gh search):

| PR | Pattern claim | Catalog ratifies? |
|---|---|---|
| #52 PR 0a | `@candidate-pattern: pattern-009-tenant-keying-retrofit (1st instance)` | YES (was candidate; now ratified) |
| #57 PR 0b | `@candidate-pattern: pattern-009-tenant-keying-retrofit (2nd instance)` | YES |
| #60 PR 0c | `@candidate-pattern: pattern-009-tenant-keying-retrofit (3rd instance)` | YES |
| #64 PR 0d | `@candidate-pattern: pattern-009-tenant-keying-retrofit (4th instance — ratification candidate)` | YES; trigger met |
| #42 cohort-2 | `@candidate-pattern: pattern-010-financial-write-path` (1st instance) | RENUMBERED to pattern-012 per V3 #2; PRs that landed BEFORE the renumber carry the original claim string |
| #51 cohort-3 | `@standing-pattern: pattern-009` (formal) + `@candidate-pattern: pattern-011-cartridge-read-via-post` | pattern-011-cartridge-read-via-post is NEW; not yet in catalog; **GAP — needs catalog add** |

### Gap 8 — pattern-011-cartridge-read-via-post candidate from V1 #1 cohort-3 hand-off

**Status:** ONR's V1 #1 cohort-3 hand-off (#51) proposed `pattern-011-cartridge-read-via-post` (Bridge POST endpoint for read-only cartridge queries; first instance: Engineer cohort-3 prereq PR 0 + FED cohort-3 PR 1).

**Catalog state:** NOT in the catalog. Conflicts with existing `pattern-011` (cross-cluster event publisher wiring).

**ONR's read:** ANOTHER number-collision (same shape as the pattern-010 collision V3 #2 resolved). ONR should renumber to `pattern-013-cartridge-read-via-post`.

**Recommended cleanup:** add to the cleanup PR — renumber the V1 #1 candidate to pattern-013 + update the cohort-3 hand-off reference (#51 is MERGED; needs amendment PR OR forward-watch note in the catalog).

---

## Coordination with QM V3 #8 addendum

V4 #6 audit (beacon protocol formalization) flagged QM V3 #8 addendum as a beacon-naming validator allowlist. Parallel observation: pattern catalog edits coordinate with QM's standing-patterns awareness (QM daemon may flag PRs whose `@standing-pattern:` / `@candidate-pattern:` claim doesn't resolve in the catalog).

**Recommendation:** Admiral coordinates cleanup PR with QM to ensure:
- Catalog edits land BEFORE QM allowlist refresh
- pattern-013-cartridge-read-via-post catalog entry lands before cohort-3 Engineer prereq PR 0 cites it

---

## What ONR does next

V4 #4 deliverable complete (this audit). Files `onr-status-*-v4-item-4-pattern-catalog-audit-complete.md`. Proceeds to V4 #5 (WS-E Phase 10 deep research; final substantive V4 item).

---

## Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T14-05Z` item #4
2. `coordination/inbox/admiral-ruling-2026-05-21T14-10Z-onr-v4-adr-authoring-option-c.md` — scope precedent for governance edits
3. `shipyard/_shared/engineering/standing-approved-patterns.md` (current catalog state)
4. `coordination/inbox/admiral-ruling-2026-05-20T19-30Z-engineer-cohort-2-pr-0a-0b-0c-unified-green-attest.md` + shipyard#63 — pattern-009-tenant-keying-retrofit ratification trigger
5. shipyard#77 (V3 #2) — pattern-012 renumber catalog edit (added candidate entry)
6. shipyard#51 (V1 #1 cohort-3 hand-off) — pattern-011-cartridge-read-via-post candidate

---

— ONR, 2026-05-21T14:18Z
