# ADR 0091 Steps 5 + 6 pre-research (2026-05-21)

**Authored by:** ONR (V5 batch item #5)
**Requester:** Admiral (per `admiral-directive-2026-05-21T14-30Z` item #5)
**Authored at:** 2026-05-21T14-38Z
**Status:** draft (sec-eng + .NET-architect council review at Engineer Step 5/6 PR opening)

---

## Scope

V3 #3 Engineer Stage-06 sequencing (shipyard#79) mapped ADR 0091 Steps 1-5 (facade introduction → DbContext rewrite → EFCore query-filter → test fixture migration → facade `[Obsolete]` + analyzer → facade deletion). What's Step 6?

Research:
- Step 5 deletion strategy (single sweep PR? per-cluster sweep? rollback?)
- Step 6 — likely `foundation-authorization` package version bump + downstream consumer migration (per ADR 0091 R2 §"Phases" derived; verify)
- Cross-cluster impact + sequencing
- Risks + mitigations

---

## TL;DR

1. **Step 5 (facade deletion) is a SINGLE sweep PR.** After one-cohort grace from Step 4 ratification, the `[Obsolete]` facade is deleted; consumers that haven't migrated fail at build (CS0246 — facade type not found). Mechanical; ~1-2h Engineer + sec-eng SPOT-CHECK MANDATORY (facade-removal is security-relevant if any consumer slipped through Step 4 analyzer).

2. **Step 6 doesn't exist in canonical ADR 0091 R2 §"Phases".** The directive's "Step 6" reference is either:
   - **Option A:** Step 6 = `foundation-authorization` v2 package-bump + downstream consumer migration (post-deletion follow-up; cleaning up the package boundary)
   - **Option B:** Step 6 is a future-watched item (analyzer-rule retirement; ratchet completion; pattern-009-tenant-keying-retrofit deprecation per ADR substrate maturity)
   - **Option C:** Step 6 is the directive's anticipation of a follow-on workstream not yet specified

3. **ONR's read:** Step 6 = Option A (foundation-authorization package boundary cleanup). Mechanical follow-up; ~3-4h Engineer; not a substantive architectural change.

4. **Rollback considerations:** Step 5 deletion is IRREVERSIBLE in the strict sense (consumer code that compiled against `Foundation.Authorization.ITenantContext` no longer compiles). But: the facade was `[Obsolete]` for one full cohort grace period before deletion, so all known consumers have migrated. Re-introduction would be a Revision 3 amendment to the ADR if needed.

5. **Cross-cluster impact:** Step 5 affects ALL packages that previously consumed the facade (~9 packages per V2 #1 consumer inventory). Build CI catches any straggler at compile-time; no runtime risk.

6. **Sequence:** Step 4 (analyzer + `[Obsolete]`) → wait one cohort grace → Step 5 (deletion) → Step 6 (package boundary cleanup). Total ~8-12 weeks post-Step-4 ship.

---

## 1. Step 5 — Facade deletion strategy

### 1.1 ADR 0091 R2 canonical spec

Per ADR 0091 R2 §"Compatibility plan — Migration path":

> **Step 5 — facade deletion.** Delete `Foundation.Authorization.ITenantContext` after one-cohort grace period from Step 4. Ratchet completes.

### 1.2 Deletion strategy

**Single sweep PR (ONR recommended):**

- One PR that:
  - Deletes `packages/foundation-authorization/ITenantContext.cs` (the facade)
  - Removes any remaining `[Obsolete]` warnings in consumer code that were tolerated during grace
  - Updates `foundation-authorization` package version (Step 6 territory; see §2)
  - Removes the TODO marker about RequestContextMixingAnalyzer (Step 4 already shipped it; comment is now obsolete)

**vs Per-cluster sweep (ALTERNATIVE):**

- Each cluster's PR removes the facade type reference one at a time
- Cleaner per-PR review surface
- Slower; more coordination overhead
- Engineer's discretion at Step 5 kickoff

**ONR's read:** single sweep is canonical. By Step 5, all consumers have already migrated (per analyzer enforcement + obsolete warnings); the deletion is mechanical removal of a no-longer-used file.

### 1.3 Rollback strategy

Step 5 is functionally irreversible:
- Consumer code that compiled against `Foundation.Authorization.ITenantContext` (the facade) no longer compiles after deletion
- Re-introducing the facade IS possible (Revision 3 amendment), but the grace-window expectation is that no consumer has the dependency

**Rollback safety net:**
- Pre-Step-5 PR: grep across the entire shipyard for any remaining `Foundation.Authorization.ITenantContext` reference → expect ZERO
- If non-zero (a straggler): block Step 5; route via `engineer-question-*` for the consumer to migrate first

### 1.4 Tests

Step 5 PR tests:
- `dotnet build` succeeds across ALL projects after facade deletion (covers compile-time gap detection)
- All existing tests pass (no regression)
- Optional: grep test (`grep -rn "Foundation.Authorization.ITenantContext"` should return EXACTLY ZERO non-ADR-citation matches; doc citations OK)

### 1.5 Council requirement

**sec-eng SPOT-CHECK MANDATORY** — facade deletion is security-relevant if any consumer slipped through the Step 4 analyzer. Sec-eng verifies the grep result + the analyzer test suite caught the obsolete-warning matrix correctly.

### 1.6 Effort

~1-2h Engineer + ~1h sec-eng SPOT-CHECK. Single PR.

---

## 2. Step 6 — Foundation-authorization package boundary cleanup (ONR's read)

### 2.1 Interpretation

ADR 0091 R2 §"Phases" canonical text stops at Step 5. The directive's V5 #5 reference to Step 6 is an extension; ONR interprets as the natural follow-up:

**Package boundary cleanup:**
- After facade deletion (Step 5), `foundation-authorization` package contains 3 interfaces (`ICurrentUser`, `IAuthorizationContext`, plus the deleted facade slot)
- Plus `DependencyInjection/TenantContextServiceCollectionExtensions.cs` + `DependencyInjection/TenantContextScopeAssertion.cs`
- Plus `Analyzers/RequestContextMixingAnalyzer.cs` (Step 4 output)

**Cleanup items:**
- Update `foundation-authorization` package version from 1.x (facade era) to 2.0 (post-facade era) — semver-major bump
- Update consumer references that pin to 1.x version
- Update `Sunfish.Foundation.Authorization.csproj` to reflect the new surface
- Update docs page (`apps/docs/foundation-authorization/overview.md`) if exists
- Optional: rename namespace if the package's intent shifted

### 2.2 Alternative interpretations of Step 6

**Option B — Analyzer-rule retirement:**

Pattern-009-tenant-keying-retrofit (ratified 2026-05-21) is a candidate for analyzer retirement once all clusters have migrated (substrate retrofit "done"). Step 6 could be the retirement PR.

Less likely than Option A; pattern-009-tenant-keying-retrofit ratification trigger met TODAY, so the retirement window is years out, not weeks.

**Option C — Follow-on workstream not yet specified:**

Directive references "Step 6" speculatively; the actual scope materializes only when ADR 0091 R2 is amended (Revision 3?) to add a Step 6 section. ONR's read: this is the most likely interpretation for the directive's vagueness — Step 6 is forward-watched, not yet specified.

### 2.3 ONR's read — recommend Option A as Step 6 spec

Step 6 = package boundary cleanup after Step 5 deletion. ~3-4h Engineer + advisory council. Mechanical follow-up.

### 2.4 Effort

If Option A:
- ~3-4h Engineer (package version bump + consumer reference updates + docs page touch)
- Advisory council (.NET-architect for the semver-major version bump)

---

## 3. Cross-cluster impact + sequencing

### 3.1 Step 4 → Step 5 timing

Per ADR 0091 R2 §"Phases":
> "Delete `Foundation.Authorization.ITenantContext` after one-cohort grace period from Step 4."

**What defines "one cohort grace"?** Ambiguous in the ADR. ONR's read (per V2 #1 open question §6 CIC #1):
- Conservative: 1 cohort = ~3-4 weeks
- Liberal: 1 cohort = ~1-2 weeks (cohort-2 → cohort-3 transition)

**ONR recommends:** Engineer + Admiral confirm at Step 4 ship — `cohort-N is the cohort during which Step 4 ships; cohort-N+1 is the grace window; Step 5 ships at start of cohort-N+2`.

### 3.2 Step 5 → Step 6 timing

Step 5 deletion is essentially atomic; Step 6 follow-up can ship in the SAME PR (or immediately after as a follow-on PR if Engineer prefers split).

ONR's read: BUNDLE Step 5 + Step 6 (Option A) into a single PR. ~3-5h total.

### 3.3 Cross-cluster impact

Step 5 deletion affects ALL packages that previously consumed the facade per V2 #1 consumer inventory:

| Package | Step 5 deletion impact |
|---|---|
| blocks-financial-ar | None (already migrated to `Foundation.MultiTenancy.ITenantContext`) |
| blocks-financial-ap | None |
| blocks-financial-payments | None |
| blocks-financial-ledger | None |
| blocks-leases | None (migration at Step 2.1) |
| blocks-businesscases | Verify migrated (was facade-consumer; may have ICurrentUser + IAuthorizationContext narrowing pending) |
| blocks-subscriptions | Same as blocks-businesscases |
| foundation/ tests | Test fixtures migrated at Step 3 |
| foundation-authorization tests | Updated alongside Step 5 |

**Expected: zero remaining facade references at Step 5 kickoff.** Step 4 analyzer + obsolete warnings should have caught any straggler.

---

## 4. Risks + mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Straggler consumer missed by Step 4 analyzer | Low | High (Step 5 fails at compile) | Pre-Step-5 grep audit; route to consumer's owner for migration before Step 5 lands |
| Grace-window too short (consumer expects 1 cohort = N weeks but gets N/2) | Medium | Medium (consumer caught off-guard) | Admiral broadcasts Step 4 ship + Step 5 expected timing at Step 4 PR open |
| Step 6 package version bump breaks downstream pinning | Medium | Medium (downstream consumer recompile) | Semver-major bump signals breaking change; consumers update at their pace |
| `[Obsolete]` warning suppressed in consumer code (allows facade use without migration signal) | Medium | High (silent dependency lingers) | Pre-Step-5 grep: check for `#pragma warning disable CS0618` (obsolete-suppression); any matches flagged for manual migration |
| Step 6 ambiguity in canonical ADR | High | Low (interpretation issue) | This research surfaces; Admiral ratifies Option A at Step 5 + 6 dispatch |
| Re-introduction of facade post-deletion is needed (rare) | Low | Medium (Revision 3 ADR amendment) | Documented in §1.3; acceptable cost vs deletion-ratchet completion |

---

## 5. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

### For .NET-architect council

1. **Step 6 interpretation — Option A (package boundary cleanup; ONR recommended) vs Option B (analyzer retirement; deferred) vs Option C (TBD)?**
2. **Bundle Step 5 + Step 6 into single PR — confirm?** ONR recommends YES; ~3-5h total; cleaner ratchet.
3. **Pre-Step-5 grep audit scope — full shipyard repo (recommended) vs cluster-by-cluster?**

### For security-engineering council

1. **Step 5 sec-eng SPOT-CHECK — MANDATORY (ONR recommended) vs advisory?** ONR's read: MANDATORY; facade-deletion is security-adjacent.
2. **`#pragma warning disable CS0618` suppression check — should it be in the Step 5 acceptance criteria? Or is the analyzer (Step 4) covering it?**

### For CIC

1. **"One cohort grace" definition — confirm (cohort-N+1 grace; Step 5 at cohort-N+2 start)?**
2. **Step 6 scope confirmation per Option A.**

---

## 6. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T14-30Z` item #5
2. V3 #3 Engineer Stage-06 sequencing (shipyard#79) — phase 5 + 6 placeholder
3. V2 #1 ADR 0091 Steps 3+4 pre-research (shipyard#68) — Step 4 analyzer + consumer inventory
4. ADR 0091 R2 (Accepted) §"Phases" canonical text
5. V5 #2 catalog hygiene PR (shipyard#88) — pattern-009-tenant-keying-retrofit promotion context

---

## 7. What ONR does next

V5 #5 deliverable complete. Files `onr-status-*-v5-item-5-adr-0091-steps-5-6-complete.md`. Proceeds to V5 #4 (W#60 P4 PR 2 detailed impl spec).

— ONR, 2026-05-21T14:38Z
