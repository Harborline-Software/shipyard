# Pattern-015 / 016 / 017 ratification readiness analysis (V14 #1)

**Authored by:** ONR (V14 batch item #1)
**Requester:** Admiral (per V14 standing-dispatch directive 2026-05-25)
**Authored at:** 2026-05-25T1430Z
**Status:** draft

---

## Scope

Cohort-3 introduced three candidate cross-page patterns in `shipyard/_shared/design/cohort-3/`
(per PR #116 MERGED 2026-05-22). The patterns are first-instance-visible on the four cohort-3
report pages but only ratify after they ship in additional distinct PRs across cohorts.

- `pattern-015-provisional-report-surface` (provisionality banner)
- `pattern-016-run-on-demand-report` (user-triggered run; no auto-fetch)
- `pattern-017-csv-export-affordance` (Export CSV button + filename convention)

This research asks: how close are each of these to ratification, where do the next instances
come from, and is there scope-creep risk that would force a lettered sub-pattern split (per the
`pattern-012a/b` precedent codified in admiral-ruling-2026-05-22T18-25Z)?

**Out of scope:** ratification of the patterns themselves (that is Admiral's call after the
threshold count is met); design alterations to the patterns; cohort-3 implementation status.

---

## TL;DR

1. **Current shipping count: 0 for all three.** Cohort-3 PRs are still pre-Stage-06 — the
   four pages that would carry first-instance claims (Trial Balance / AR Aging / P&L by
   Property / Rent Roll) are at design direction (PR #116 MERGED) and substrate-ready (W#77)
   but no FED build PRs have opened. The candidate-pattern docs in
   `shipyard/_shared/design/cohort-3/` are visible-signature definitions, not shipped
   instances. Cohort-3 first-instance shippings are the ratification clock's t=0.

2. **Ratification trigger: 2nd distinct cohort instance.** Per each pattern doc's "Ratification
   trigger" section, the threshold is the **second cohort** carrying a `@candidate-pattern:`
   claim. This is a faster-ratification posture than the canonical 3-shipping threshold used
   for pattern-001..014 — appropriate because these are visible-signature design patterns
   (UX-shape consistency), not architectural patterns (substrate-correctness consistency).

3. **Cohort-4 ratifies all three on AP Aging if the cohort ships intact.** Per the cohort-4
   PRE-SCOPE doc (`shipyard/_shared/design/cohort-4/PRE-SCOPE.md`), AP Aging mirrors AR Aging
   structurally and inherits all three patterns directly. AP Aging is the canonical second-
   instance carrier. If cohort-4 ships AP Aging + at least one additional cartridge page (Cash
   Flow Statement, Owner Statement, Balance Sheet), the patterns ratify on the AP Aging instance
   and the additional cartridge would be a third-instance early-confidence signal.

4. **Cohort-5 (per ONR V5 #1a recommendation: ARR/MRR reporting) is a partial ratification
   carrier.** ARR / MRR / Cohort Retention pages SHOULD inherit pattern-016 (run-on-demand) and
   COULD inherit pattern-015 (provisionality) if the substrate exposes time-windowed
   provisionality. ARR/MRR pages will likely NOT inherit pattern-017 (CSV export) at first ship
   — they are dashboard-style live tiles, not export-grade reports. Cohort-5 is a 3rd-instance
   carrier for pattern-015 (if substrate cooperates) and pattern-016 (definitely); not a
   carrier for pattern-017.

5. **Scope-creep risk: LOW for pattern-016 and pattern-017; MEDIUM for pattern-015.**
   Pattern-015's "provisionality" semantic carries ambiguity: a report that shows partial
   results during pagination IS provisional in one sense (data is incomplete), but the doc's
   intent is provisionality of the underlying data (e.g., AR Aging based on a not-yet-closed
   period). A second instance from a different domain (e.g., MRR with mid-month
   accumulator-not-yet-flushed semantics) could surface that ambiguity and trigger a 015a /
   015b split. Forward-watch this on the cohort-5 ARR/MRR first ship.

6. **Pattern-017 has the cleanest ratification path.** CSV export is mechanical; the
   `<ExportCsvButton>` component + filename convention transfer 1:1 to any cartridge-backed
   page. AP Aging in cohort-4 is the canonical second instance. No split risk identified.

7. **Pattern numbering correction surfaced.** The PR #116 commit message refers to the
   candidate patterns as `pattern-011 / 012 / 013` (the slot numbers reserved at that
   document's authoring time). The canonical pattern catalog (`shipyard/_shared/engineering/
   standing-approved-patterns.md`) already uses slots 011 (cross-cluster event publisher
   candidate), 012a/b (write-path candidates), 013 (cartridge-read-via-post candidate), and
   014 (cross-tenant audit emission formal). The cohort-3 candidate-pattern docs (the
   `.md` files themselves) correctly use **015 / 016 / 017** as the next available slots. The
   PR #116 commit-message description is stale; downstream agents should read the pattern doc
   bodies for canonical numbering, not the commit-message summary.

---

## 1. Pattern-by-pattern instance accounting

### 1.1 pattern-015-provisional-report-surface

**Definition (canonical, from `_shared/design/cohort-3/provisionality-banner-pattern.md`):**
Provisionality banner UX that surfaces `ReportRunResult.IsProvisional = true` from the
report cartridge envelope. Visible signature: yellow / amber banner above the report header
with text "Provisional results — period not yet closed" + cohort-3 `tokens.md`
`provisional-surface` token composition.

**Shipping count: 0.** Cohort-3 PRs not yet opened. The four cohort-3 pages (Trial Balance /
AR Aging / P&L by Property / Rent Roll) carry the pattern in design direction (PR #116) but
the FED Stage-06 implementation PRs are pending.

**Ratification trigger:** second cohort using `IsProvisional` semantics ships clean carrying
`@candidate-pattern: pattern-015` claim.

**Likely next instances (forward-watch order):**

| Cohort | Page | Pattern fit | Confidence |
|---|---|---|---|
| Cohort-3 (in-flight) | All 4 report pages | First instance (the source) | ~100% |
| Cohort-4 (PRE-SCOPE) | AP Aging | Mirror of AR Aging; same `ReportRunResult` envelope | ~95% |
| Cohort-4 (speculative) | Cash Flow Statement / Owner Statement / Balance Sheet | Same envelope; provisionality applies | ~70% (substrate-dependent) |
| Cohort-5 (V5 #1a recommended: ARR/MRR) | MRR Detail page | If substrate exposes `IsProvisional` (mid-period accumulator) | ~50% (semantic question) |

**Scope-creep risk:** MEDIUM. The semantic "provisional" carries two distinct meanings:

- **Meaning A (cohort-3 intent):** the report period is not yet closed; numbers may change
  as transactions land before the close. This is the AR/AP/Trial-Balance/P&L flavor — the
  data is COMPLETE for the queried period, but the period itself is mid-flight.
- **Meaning B (potential cohort-5 variant):** the underlying accumulator has not yet flushed
  recent activity; numbers may differ from a same-instant authoritative recompute. This is
  the MRR / ARR / time-windowed-aggregator flavor — the data is INCOMPLETE for the queried
  instant because the read-side is eventually consistent.

If cohort-5's ARR/MRR pages ship with `IsProvisional` semantics interpreted as Meaning B,
sec-eng or .NET-architect could RED the PR for "this isn't the same pattern; users will
read the same banner and infer the same thing about both." The recommended posture, if
this ambiguity surfaces, is a **pattern-015a / 015b split** modeled on pattern-012a/b:

- `pattern-015a-period-provisional-report-surface` — Meaning A (period not closed)
- `pattern-015b-snapshot-provisional-report-surface` — Meaning B (accumulator not flushed)

Both could use the same visible-signature banner with slightly different copy. The split
preserves the UX consistency while distinguishing the substrate semantics. **Pre-stage this
split decision for Admiral on the cohort-5 first-ARR/MRR PR open** — not before.

**Recommendation:** do NOT pre-emptively split. Ratify on cohort-4 AP Aging (Meaning A
second instance). Forward-watch cohort-5 ARR/MRR for Meaning B emergence. If it emerges,
file an `admiral-ruling-*-pattern-015-split` beacon at that point. If it doesn't emerge
(cohort-5 ARR/MRR substrate exposes `IsProvisional = false` always, or never exposes the
field), pattern-015 stays single and ratifies cleanly.

### 1.2 pattern-016-run-on-demand-report

**Definition (canonical, from `_shared/design/cohort-3/run-on-demand-pattern.md`):**
Report pages are explicitly user-triggered ("Run" button). No auto-fetch on page mount.
Empty-state until Run is clicked. Filter-bar parameters frozen at Run-click time.
State machine: empty → running (loading) → success (results) | failure (ErrorSurface) | reset.

**Shipping count: 0.**

**Ratification trigger:** second user-triggered report ships clean carrying
`@candidate-pattern: pattern-016` claim.

**Likely next instances:**

| Cohort | Page | Pattern fit | Confidence |
|---|---|---|---|
| Cohort-3 (in-flight) | All 4 report pages | First instance | ~100% |
| Cohort-4 (PRE-SCOPE) | AP Aging | Direct mirror | ~95% |
| Cohort-4 (speculative) | Additional cartridges | Direct mirror | ~80% |
| Cohort-5 (ARR/MRR) | ARR Dashboard + MRR Detail + Cohort Retention | Strong fit (these are reports, not real-time tiles per V5 #1a anti-pattern para) | ~85% |
| Cohort-N (forecast) | Schedule-E / 1099 / Owner statements | Same shape | ~90% |

**Scope-creep risk: LOW.** Pattern-016 has a clean negative-criteria boundary (no auto-fetch;
no polling; no live tiles). Forward-watch: if a dashboard tile is added to one of the report
pages that DOES auto-fetch (e.g., "current period snapshot" on the AR Aging page header), it
should be a separate sub-pattern (e.g., `pattern-016-companion-live-tile`) — but that's a
future concern.

**Recommendation:** ratify on cohort-4 AP Aging (second instance from same domain). No
split anticipated.

### 1.3 pattern-017-csv-export-affordance

**Definition (canonical, from `_shared/design/cohort-3/csv-export-pattern.md`):**
`<ExportCsvButton>` shipped in cohort-3 PR 1 shared-infra. Filename convention:
`<report-kind>-<tenant-slug>-<period-marker>.csv`. Button position: top-right of the report
chrome, alongside Run. Only enabled in `success` state. CSV is the run-result, not a re-fetch.

**Shipping count: 0.**

**Ratification trigger:** next non-report CSV export surface ships clean carrying
`@candidate-pattern: pattern-017` claim.

**Likely next instances:**

| Cohort | Page | Pattern fit | Confidence |
|---|---|---|---|
| Cohort-3 (in-flight) | All 4 report pages | First instance (cluster) | ~100% |
| Cohort-4 (PRE-SCOPE) | AP Aging | Direct mirror | ~95% |
| Cohort-5 (ARR/MRR) | (NOT a carrier) | ARR/MRR is dashboard-style; export is forward-watch per V5 #1a | ~30% |
| Cohort-N forecast | Audit Events list export | Maybe — would be a non-report CSV; clean second-domain instance | ~60% (if FED prioritizes export on cohort-4 audit-trail surface) |
| Cohort-N forecast | ERPNext deletion finale | No fit | ~0% |

**Scope-creep risk: LOW.** CSV export is the cleanest of the three patterns:
mechanical button + filename convention + state-machine gate. The forward-watch named in
the pattern doc — "Other export formats (PDF, XLSX, JSON) — pattern-017 is CSV-only" —
correctly future-proofs against scope creep by reserving the right to add a separate
pattern for additional formats.

**Recommendation:** ratify on cohort-4 AP Aging (second cohort, same domain) OR — IF the
cohort-4 audit-trail surface adds CSV export — that's a stronger second-instance signal
because it's a different domain (audit events, not financial reports). Cohort-4 audit events
on sunfish#71 currently does NOT include CSV export per the spec; if FED adds it as a
quick win in cycle 2 or a follow-on, that surface would carry `pattern-017` cleanly and the
pattern ratifies on a cross-domain signal (better than same-domain repeat).

---

## 2. Cohort-4 as the canonical ratification carrier

The cohort-4 PRE-SCOPE doc (`shipyard/_shared/design/cohort-4/PRE-SCOPE.md`, authored
2026-05-22 by PAO) explicitly flags all three patterns as cohort-4 candidates:

| Pattern | Cohort-3 candidate | Cohort-4 expected use |
|---|---|---|
| pattern-015 | Candidate | Likely ratifies on AP Aging second instance |
| pattern-016 | Candidate | Likely ratifies on AP Aging second instance |
| pattern-017 | Candidate | Likely ratifies on AP Aging second instance |

The PRE-SCOPE doc names cohort-4 explicitly as the "cohort that completes cohort-3's pattern-
promotion arc" — this is the canonical ratification path. AP Aging is the trigger.

**Sequencing dependency:** cohort-3 must merge before cohort-4 AP Aging ships, because the
shared components in cohort-3 PR 1 (`<ProvisionalityBanner>`, `<ExportCsvButton>`,
`<ReportFilterBar>`, `<ChartSelector>`, `<RunButton>`) are AP Aging's consumption surface.
Cohort-3 PR 1 ships these primitives, so AP Aging consumption is import-and-go.

**If cohort-4 splits into multiple cartridges (additional Cash Flow / Owner Statement /
Balance Sheet pages):** each adds an additional instance. Ratification doesn't require all
three; one second-cohort instance is sufficient per the doc-defined threshold.

---

## 3. Cohort-5 as a partial carrier

ONR V5 #1a (PR #95 MERGED) recommends cohort-5 anchor = ARR/MRR reporting wave. The
proposed cluster: ARR Dashboard + MRR Detail + Cohort Retention Chart, ~5 PRs.

**Pattern carriage forecast:**

| Pattern | Carries on ARR/MRR? | Notes |
|---|---|---|
| pattern-015 | Yes IF substrate exposes `IsProvisional` on the accumulator envelope | See §1.1 split-risk discussion |
| pattern-016 | Yes (these are reports, not live tiles per V5 #1a anti-pattern carve-out) | Cohort-5 = 3rd-instance carrier; pattern-016 will have ratified on cohort-4 |
| pattern-017 | No (dashboard-style; CSV export forward-watched per V5 #1a) | If V5 #1a is amended to include CSV export, would be 3rd-instance |

Net cohort-5 contribution: 1 reliable instance (pattern-016 3rd-instance, low-stakes since
already ratified on cohort-4); 1 conditional instance (pattern-015, may trigger split risk);
0 instances for pattern-017.

---

## 4. Cross-pattern interactions

The cohort-3 pattern docs explicitly call out cross-pattern interactions:

- **pattern-015 + pattern-016:** "Pattern-015 ratifies independently of pattern-016 and
  pattern-017. A future surface could use provisionality without being run-on-demand (e.g.,
  a dashboard tile auto-refreshing every 5min that still shows `isProvisional`)." This is
  forward-thinking: it pre-authorizes a pattern-015-only carrier (no pattern-016 claim) and
  preserves ratification independence.

- **pattern-016 + pattern-015:** "Pattern-011 [015] (provisionality banner) — appears only
  in SUCCESS state; complements pattern-016 but ratifies independently." (NB: the cohort-3
  doc still uses the old `pattern-011` numbering; the canonical numbering is 015.)

- **pattern-017 + pattern-009:** "Pattern-009 (Bridge endpoint + frontend rebind pair) — the
  export endpoint is *another* Bridge endpoint pair (one for run, one for export, with the
  same contract shape); pattern-009 applies to both." This pin matters for ratification
  accounting: each cohort-3 page exercises pattern-009 twice (run + export) AND pattern-017
  once. Pattern-009 (already formal) doesn't accumulate ratification credit; pattern-017
  does.

**Net:** the patterns are well-decomposed. No hidden dependencies that would force joint
ratification.

---

## 5. Lettered sub-pattern split decision matrix

Following the pattern-012a/b precedent (admiral-ruling-2026-05-22T18-25Z), a candidate
pattern splits into lettered sub-patterns when:

1. **Two distinct architectural shapes are emerging under the same candidate slot** AND
2. **The shapes' negative-criteria boundaries differ materially** AND
3. **Future PRs would need to choose between the two shapes (not just sit in both)**.

**Pattern-015:** condition 1 partially true (Meaning A vs Meaning B as discussed in §1.1);
condition 2 likely true (period-provisional vs snapshot-provisional are different substrate
semantics); condition 3 unclear (future PRs would likely choose Meaning A almost always; Meaning
B is rare). **Conclusion: defer split decision; forward-watch only.**

**Pattern-016:** none of the three conditions are met. **No split anticipated.**

**Pattern-017:** none of the three conditions are met. **No split anticipated.**

---

## 6. Forward-watch checklist for Admiral

| Event | Action |
|---|---|
| First cohort-3 FED build PR opens | Verify it carries `@candidate-pattern: pattern-015/016/017` claims explicitly in PR description |
| Cohort-3 first page (likely Trial Balance per substrate readiness order) ships | First-instance shipping count = 1 for all three patterns |
| Cohort-4 AP Aging ships | Second-instance shipping count = 2 for all three patterns → ratification trigger reached |
| Cohort-4 SPOT-CHECK on AP Aging | sec-eng + .NET-architect verify the patterns shipped clean (no deviations); IF clean, ratify all three; IF deviation, hold |
| Cohort-5 ARR/MRR ships with `IsProvisional` semantics | If Meaning A → routine 3rd instance; if Meaning B → file `admiral-ruling-*-pattern-015-split` for 015a/015b decision |
| Cohort-4 audit-trail surface adds CSV export | Cross-domain second-instance for pattern-017 — stronger ratification signal than same-domain AP Aging repeat |

---

## 7. Open questions for Admiral

1. **Numbering reconciliation.** The pattern-015/016/017 numbering is canonical per the
   cohort-3 design docs; the PR #116 commit message uses stale 011/012/013. Should
   ONR file a follow-up `docs(_shared)` PR amending the cohort-3 commit-message description
   for archival clarity, or leave the commit history as-is and note the discrepancy via this
   research doc only?

2. **Ratification authority for design patterns.** Pattern-001..014 ratify on
   architectural-pattern shipping (sec-eng + .NET-architect council). Pattern-015/016/017
   are visible-signature design patterns — does ratification require the same council
   verdicts, or is PAO's design-direction review sufficient? Current default per the pattern
   doc text is the 3-shipping threshold (now relaxed to 2-cohort threshold per the doc
   bodies); council review is implied via the cohort-N SPOT-CHECK, but explicit ratification
   ownership is not pinned. Admiral ruling recommended before cohort-4 AP Aging ships.

3. **Pattern-015 Meaning A vs Meaning B forward-watch.** Sec-eng / .NET-architect / PAO
   alignment on what "provisionality" connotes substrate-wise. Recommended pre-stage:
   PAO + ONR pre-author a single sentence in the pattern-015 doc clarifying which Meaning
   is in scope at ratification time, with explicit forward-watch language for the other
   Meaning. This avoids the split decision being made under PR-clock pressure when ARR/MRR
   ships.

---

## 8. Sources cited

1. `shipyard/_shared/design/cohort-3/provisionality-banner-pattern.md` (canonical
   pattern-015 visible-signature definition; cohort-3 design direction PR #116 MERGED
   2026-05-22)
2. `shipyard/_shared/design/cohort-3/run-on-demand-pattern.md` (canonical pattern-016
   definition; same PR)
3. `shipyard/_shared/design/cohort-3/csv-export-pattern.md` (canonical pattern-017
   definition; same PR)
4. `shipyard/_shared/design/cohort-4/PRE-SCOPE.md` (cohort-4 PRE-SCOPE; PAO 2026-05-22;
   explicit pattern-015/016/017 forecast)
5. `shipyard/_shared/engineering/standing-approved-patterns.md` (canonical pattern catalog;
   current pattern slot inventory)
6. `shipyard/icm/01_discovery/research/cohort-5-scope-survey-2026-05-21.md` (ONR V5 #1a;
   ARR/MRR recommended cohort-5 anchor)
7. `shipyard/icm/01_discovery/research/cohort-6-scope-survey-2026-05-21.md` (ONR V5 #1b;
   AP Aging recommended cohort-6 anchor — BUT note that cohort-4 PRE-SCOPE pre-empts this
   and pulls AP Aging earlier)
8. `coordination/inbox/admiral-ruling-2026-05-22T18-25Z-pattern-012-split-into-012a-012b-supersedes-17-35Z.md`
   (precedent for lettered sub-pattern splits)
9. ONR V7 #6 pattern catalog snapshot (shipyard PR #108 OPEN) — prior drift audit
10. `coordination/inbox/feedback_pattern_012_multiple_collision_claims.md` (memory note —
    grep prior PRs before claiming a numeric pattern slot; the same discipline applies to
    015/016/017)

---

## 9. What ONR does next

V14 #1 deliverable complete. Files `onr-status-2026-05-25T1430Z-v14-1-pattern-015-016-017-readiness-complete.md` naming this deliverable path. Proceeds to V14 #2 (cohort-5 scope
survey) as next batch item.

— ONR, 2026-05-25T14:30Z
