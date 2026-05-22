# Pattern catalog 2026-05-22 snapshot + drift audit

**Authored by:** ONR (V7 batch item #6)
**Requester:** Admiral (per `admiral-directive-2026-05-22T12-45Z` item #6)
**Authored at:** 2026-05-22T13-00Z

---

## Scope

Capture current state of `_shared/engineering/standing-approved-patterns.md` as of 2026-05-22T13-00Z. Identify drift between catalog entries and PR claims. Specifically verify pattern-014 "4 instances; should ratify NOW" claim from directive and pattern-010/012 hold-pending-3rd-instance status.

---

## TL;DR

1. **10 ratified formal patterns** in main catalog (pattern-001 through pattern-009 + pattern-009-tenant-keying-retrofit). pattern-009-tenant-keying-retrofit promoted via shipyard#103 (MERGED 2026-05-22T07:26Z); ONR's parallel V5 #2 (#88) still OPEN — superseded.

2. **3 candidate patterns on main:** pattern-004 (cluster aging; 2 instances; needs 1 more), pattern-010 (docs-toc; bundled with pattern-006), pattern-011 (cross-cluster event publisher; 0 instances).

3. **3 candidate patterns referenced but NOT on main:**
   - pattern-012-financial-write-path (V3 #2; cohort-2 PR 3 = 1st instance)
   - pattern-013-cartridge-read-via-post (V1 #1 cohort-3 hand-off; renumbered from pattern-011 collision)
   - pattern-014-bridge-cross-tenant-audit-emission (Admiral directive 2026-05-21T16-25Z claims; **`gh pr list --search "pattern-014 in:title,body"` returns ZERO MERGED PRs** — claim unverified)

4. **DRIFT: pattern-014 "4 instances; should ratify NOW" UNVERIFIABLE.** No merged PRs tag pattern-014. Engineer's audit-emission Bridge retrofit PRs are the likely source but the claims aren't documented in PR bodies/titles. **Catalog hygiene gap.**

5. **DRIFT: ONR's V5 #2 PR #88 still OPEN.** Adds pattern-012 + pattern-013 candidate entries to the catalog. Admiral's #103 (pattern-009-tenant-keying-retrofit only) MERGED 2026-05-22T07:26Z but didn't pick up #88's other adds. #88 needs supersession path (rebase OR close-as-superseded + new PR with deltas).

6. **DRIFT: pattern-010/012 "3rd instance hold" status unverified.** V3 #2 named pattern-012's 1st instance as cohort-2 PR 3. V2 #4 named cohort-4 audit-trail viewer + W#60 P4 PR 2 as ratification candidates. No 3rd-instance pattern-012 PRs shipped yet → status is still "hold pending 3rd instance" but neither candidate has shipped its pattern-012 claim.

---

## 1. Formal catalog (10 ratified patterns; main catalog lines 50-329)

| # | Pattern | Ratified date | Instances |
|---|---|---|---|
| 1 | `pattern-001` Cluster scaffold + Repository + DI | (pre-restructure baseline) | many |
| 2 | `pattern-002` ERPNext importer Pass 1 | (pre-restructure baseline) | several |
| 3 | `pattern-003` Cluster posting service | (pre-restructure baseline) | several |
| 4 | `pattern-004` Cluster aging service | listed formal in main catalog (line 148) BUT ALSO listed candidate (line 360) — DRIFT noted in V4 #4 audit; not resolved by #103 promotion | 2 instances per candidate-section note |
| 5 | `pattern-005` DI extension `Add<Block>()` umbrella | (pre-restructure) | many |
| 6 | `pattern-006` `apps/docs/blocks/<cluster>/overview.md` | (pre-restructure) | many |
| 7 | `pattern-007` Ledger row flip | (pre-restructure) | many |
| 8 | `pattern-008` Docs page touch-up | (pre-restructure) | many |
| 9 | `pattern-009` Bridge endpoint + companion frontend binding pair | 2026-05-17 (cohort-1 PR 3 trigger) | 6+ (cohort-1 PR 1-3; cohort-2 PR 1-3) |
| 10 | `pattern-009-tenant-keying-retrofit` Tenant-keying retrofit pass | 2026-05-22T07:26Z (via #103; per cohort-2 PR 0a-d cluster shipping) | 4 (cohort-2 PR 0a-d) |

---

## 2. Candidates on main catalog (lines 357-362)

| # | Pattern | Status | Shipping count |
|---|---|---|---|
| 4 | `pattern-004` (Cluster aging service) | candidate AND formal — DUPLICATED ENTRY | 2 instances; needs 1 more (per candidate note line 360) |
| 10 | `pattern-010` (`apps/docs/blocks/<cluster>/toc.yml` entry) | candidate; bundled with pattern-006 | 0 standalone |
| 11 | `pattern-011` (Cross-cluster event publisher wiring) | candidate; needs 3 shippings | 0 instances |

---

## 3. Candidates REFERENCED in PRs/directives but NOT on main catalog

### 3.1 `pattern-012-financial-write-path`

**Origin:** V3 #2 catalog renumber (shipyard#77 MERGED) — renamed from `pattern-010-financial-write-path` (cohort-2 hand-off #42 §3.27) to avoid pattern-010/pattern-011 number collision.

**1st instance:** cohort-2 PR 3 RentCollection POST (per ONR V3 #2 + V2 #4 research).

**Status:** HOLD pending 3rd instance. Both sec-eng + .NET-architect councils HOLD on ratification.

**Cataloged in ONR V5 #2 PR #88 (still OPEN).** Not yet on main.

**Recommended action:** rebase/refresh #88 OR new PR adding pattern-012 + pattern-013 candidate entries.

### 3.2 `pattern-013-cartridge-read-via-post`

**Origin:** V1 #1 cohort-3 hand-off (shipyard#51 MERGED) — proposed as `pattern-011-cartridge-read-via-post` originally; renumbered to pattern-013 per V4 #4 audit (collision with existing pattern-011 event publisher).

**1st instance:** Engineer cohort-3 prereq PR 0 + FED cohort-3 PR 1 (`POST /api/v1/reports/{kind}` cartridge runner endpoints).

**Status:** candidate; needs 3 shippings.

**Cataloged in ONR V5 #2 PR #88 (still OPEN).** Not yet on main.

### 3.3 `pattern-014-bridge-cross-tenant-audit-emission`

**Origin:** Admiral directive `admiral-directive-2026-05-21T16-25Z-engineer-v1-batch-substrate-ladder-and-tauri.md` line 50 — Engineer's V1 batch substrate ladder + audit retrofit; tagged as candidate.

**1st instance (claimed):** Engineer's V1 audit retrofit PRs (Phase 1 audit-emission helper).

**Directive claim:** V7 #6 directive line 76 — "pattern-014 (4 instances; should ratify NOW)"

**Verification:** `gh pr list --search "pattern-014 in:title,body"` returns **ZERO MERGED PRs**. Either:
- (a) Engineer shipped 4 instances without tagging pattern-014 in titles/bodies (catalog hygiene gap)
- (b) Directive claim is aspirational / forward-looking ("should ratify when 4 ship") not retrospective
- (c) Instances may exist in different repos (not searched: signal-bridge, sunfish)

**Recommended action:** Admiral verifies the 4-instance claim against Engineer's recent PR shipping log; if confirmed, file ratification-promotion catalog edit.

**Cross-search for pattern-014:**

```bash
gh pr list --repo Harborline-Software/shipyard --state merged --search "pattern-014" → 0
gh pr list --repo Harborline-Software/signal-bridge --state merged --search "pattern-014" → not searched
```

ONR did NOT exhaustively search across all repos. Admiral confirmation needed.

---

## 4. Drift findings

### Drift 1 — pattern-014 4-instance claim unverified

Per §3.3 — `gh pr list --search "pattern-014 in:title,body"` returns ZERO in shipyard. Engineer's audit retrofit PRs may have shipped instances without tagging; this is a catalog hygiene gap.

**Resolution:** Admiral verifies, file catalog-promotion PR if confirmed.

### Drift 2 — pattern-004 duplicated (formal AND candidate)

V4 #4 audit (shipyard#85) flagged this; #103 promotion didn't address it. Line 148 (formal) AND line 360 (candidate "needs 1 more shipping") both present. Lingering hygiene gap.

**Resolution:** remove candidate-section pattern-004 entry; formal entry at line 148 is canonical.

### Drift 3 — ONR V5 #2 #88 superseded

Admiral's #103 picked up pattern-009-tenant-keying-retrofit promotion but didn't pick up V5 #2's other edits (pattern-012 + pattern-013 candidate adds; path migration sweep; XO/CO role sweep). #88 still OPEN with `mergeable: UNKNOWN`.

**Resolution:** #88 needs either (a) rebase + supersession of #103 conflict, OR (b) close as superseded + new smaller PR for the un-superseded delta (pattern-012/013 candidates + path sweep + role sweep).

### Drift 4 — pattern-010/012 hold-pending-3rd-instance status verification

V2 #4 + V3 #2 named candidates for pattern-012's 2nd + 3rd instances. Current shipping log:
- 1st instance: cohort-2 PR 3 RentCollection POST (shipped per cohort-2 substrate cascade)
- 2nd instance: TBD (cohort-3 doesn't ship pattern-012 — reports are read-only POST per pattern-013)
- 3rd instance candidate: W#60 P4 PR 2 JournalEntry POST (V3 #2 + V5 #4 implementation spec; not yet shipped)

**Status as of 2026-05-22T13:00Z:** hold pending 2nd + 3rd instance. Both still pending Engineer execution.

### Drift 5 — V5 #2 catalog hygiene not yet realized

V4 #4 audit identified 8 gaps; V5 #2 PR #88 addressed them. Admiral's #103 cherry-picked the pattern-009-tenant-keying-retrofit promotion only. Remaining gaps (path migration, role attribution, pattern-012/013 candidates, pattern-004 dedup, "Last audited" date stamp) are STILL DRIFT.

**Resolution:** refresh + supersede #88 OR new cleanup PR for remaining 5 gaps.

---

## 5. Recommended cleanup actions (post-snapshot)

1. **Verify pattern-014 4-instance claim with Admiral** (file `onr-question-*` if Admiral hasn't confirmed)
2. **Refresh or supersede ONR V5 #2 #88** to land remaining catalog hygiene edits
3. **Promote pattern-014 to formal IF Admiral confirms 4 instances** (catalog edit)
4. **Remove duplicate pattern-004 candidate entry** (single-line edit)
5. **Add "Last audited: 2026-05-22" stamp** to catalog (per V4 #4 recommendation; never applied)

---

## 6. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T12-45Z-onr-v7-batch-deferred-heavy.md` item #6
2. `shipyard/_shared/engineering/standing-approved-patterns.md` (current state; verified 2026-05-22T13:00Z)
3. shipyard#103 (Admiral parallel pattern-009-tenant-keying-retrofit promotion; MERGED 2026-05-22T07:26Z)
4. shipyard#88 (ONR V5 #2 catalog hygiene; OPEN; superseded by #103 for the promotion piece)
5. V4 #4 audit (shipyard#85; OPEN) — 8 gaps identified
6. V3 #2 catalog renumber (shipyard#77; OPEN) — pattern-010→012 rename + pattern-012 candidate add
7. V1 #1 cohort-3 hand-off (shipyard#51 MERGED) — pattern-013-cartridge-read-via-post origin
8. `coordination/inbox/admiral-directive-2026-05-21T16-25Z-engineer-v1-batch-substrate-ladder-and-tauri.md` line 50 — pattern-014 candidate origin

---

## 7. What ONR does next

V7 #6 deliverable complete. Files `onr-status-*-v7-item-6-pattern-catalog-snapshot-complete.md` + open question about pattern-014 verification. Proceeds to V7 #2 (cross-cohort dependency graph).

— ONR, 2026-05-22T13:00Z
