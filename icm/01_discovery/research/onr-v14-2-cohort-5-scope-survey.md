# Cohort-5 scope survey — V14 refresh (2026-05-25)

**Authored by:** ONR (V14 batch item #2)
**Requester:** Admiral (per V14 standing-dispatch directive 2026-05-25)
**Authored at:** 2026-05-25T1530Z
**Status:** draft
**Supersedes:** ONR V5 #1a `cohort-5-scope-survey-2026-05-21.md` is now ~4 days stale; this
refresh integrates the post-cohort-4-cycle-2 evidence and reconsiders alternatives surfaced in
the V14 directive (Property Mgmt / Payments / Compliance / Self-Service).

---

## Scope

Cohort-3 (reports cluster) is in flight; cohort-4 (audit-trail viewer + AP Aging mirror) is
landing — sec-eng cycle-2 GREEN attested 2026-05-25T13:12Z on sunfish#71. Cohort-4 closes
imminently. What's cohort-5?

V14 directive named four new candidate clusters worth scoring:
- Property Management cluster (units / leases / vacancy admin)
- Payments cluster (recurring auto-debit / payment plans)
- Compliance cluster (audit log surfaces / regulator reports)
- Tenant Self-Service cluster

ONR also reconsiders the V5 #1a recommendation (ARR/MRR reporting wave) given the 4-day
gap and shifted readiness signals.

**Out of scope:** the cohort-5 decision itself (that is CIC's call); detailed Stage-05
plan authoring (that is post-decision); per-page design direction (PAO's Track C).

---

## TL;DR

1. **Refresh recommendation:** ONR's primary recommendation shifts from the V5 #1a ARR/MRR
   pick to a **two-cluster pair: cohort-5 anchor = Property Management cluster + cohort-5
   ALT scope = AP Aging trailing-second instance**. AP Aging is small (4-6h per V5 #1b);
   bundling it into cohort-5 closes the reports-cluster narrative cleanly and ratifies all
   three cohort-3 candidate patterns (015/016/017) — net cohort-5 value is materially
   higher than ARR/MRR-alone.

2. **Why the shift from V5 #1a:** four factors.
   - **Substrate readiness:** ARR/MRR needs a `blocks-subscriptions` accumulator that does
     not exist; AP Aging needs only the `ApAgingSummaryCartridge` mirror of an already-
     shipped cartridge (~3-4h Engineer).
   - **Pattern catalog hygiene:** cohort-5 with AP Aging would ratify pattern-015/016/017
     in a single cohort-pass — the institutional record gain is clean.
   - **MVP-demo critical-path:** Property Management cluster is the literal G-1 done-
     condition ("CIC's property management business runs on the Sunfish ERP app") — see
     MASTER-PLAN.md §G-1. ARR/MRR is an investor-positioning gain; Property Mgmt is
     business-validation.
   - **Risk profile:** ARR/MRR introduces a novel substrate primitive (the accumulator) +
     a novel UX surface (cohort retention chart). Property Mgmt's surfaces are mostly
     mirrors of existing patterns (vacancy listing = listings extension; unit detail =
     property detail clone).

3. **Cohort-5 = Property Management cluster (proposed scope):** Vacancies admin page +
   Unit list + Unit detail + (optional) Vacancy listing FED-side rebind cleanup. ~8-12h
   total. AP Aging bundled as a tail-end cluster member: +5-7h. Net ~13-19h, comparable to
   V5 #1a's ARR/MRR estimate.

4. **Cohort-5 alternatives:**
   - **Payments cluster** (recurring auto-debit / payment plans): blocked by ADR
     work and ERPNext-native Stripe disposition (MASTER-PLAN WS-D is "LIKELY SUPERSEDED");
     do NOT recommend until CIC confirms WS-D direction.
   - **Compliance cluster** (audit log surfaces / regulator reports): cohort-4 already
     shipped the audit log surface (sunfish#59 + sunfish#71 pair); incremental compliance
     surfaces are a smaller cohort — fits better as cohort-6 or cohort-7.
   - **Tenant Self-Service cluster** (tenant portal / magic-link / messaging): blocked on
     W#20 phases 4-9 ("WS-E") which is being re-authored per admiral directive
     2026-05-17T23-15Z; the magic-link substrate is not yet ratified.
   - **ARR/MRR (V5 #1a default):** still viable; demote to cohort-6 candidate.

5. **Cohort-5 sequencing:** depends on cohort-3 closure (reports cluster MERGED) +
   cohort-4 closure (audit-trail viewer MERGED + AP Aging shipped). Both expected within
   24-48h based on current PR-state.

6. **Per pattern claims for cohort-5 Property Mgmt:** `@standing-pattern: pattern-009` on
   every page rebind PR; `@candidate-pattern: pattern-013-cartridge-read-via-post` on any
   cartridge-backed read endpoint added; no new pattern claims anticipated (Property Mgmt
   is mostly mirror-of-existing-shape).

7. **Stage-05 cohort-5 plan should encode the cycle-1/cycle-2 sub-cohort pattern from
   cohort-4** (see V14 #4 for methodology). Cross-repo wire-contract PRs in cohort-5 should
   ship Engineer-first (DTO ships) then FED-aligned (consumes DTO).

---

## 1. Candidate scoring matrix

Refreshed scoring per V14 directive. Scores weight: substrate readiness (0-3), effort
fit (0-3), MVP-demo value (0-3), strategic positioning (0-3). Max = 12.

| Candidate | Substrate | Effort | MVP value | Strategic | Score | Notes |
|---|---|---|---|---|---|---|
| **PROPOSED: Property Mgmt + AP Aging bundle** | 3 (mostly mirrors) | 2 (13-19h moderate) | 3 (literal G-1 done-condition) | 3 (closes reports cluster + advances property-mgmt) | **11/12** | Recommended cohort-5 anchor |
| ARR/MRR (V5 #1a default) | 1 (accumulator gap) | 2 (12-20h) | 2 (investor-grade; not business-validation) | 2 (SaaS-positioning vs business-running) | 7/12 | Demote to cohort-6 |
| AP Aging alone | 3 (cartridge mirror) | 3 (5-7h small) | 2 (closes reports cleanly) | 2 (incremental) | 10/12 | Bundle into cohort-5 (recommended) OR standalone cohort-6 (V5 #1b backup) |
| Payments cluster (recurring + plans) | 0 (ADR work needed; WS-D superseded?) | 1 (15-25h novel) | 2 (transactional) | 2 (revenue-cycle dep) | 5/12 | Blocked; need CIC decision on WS-D |
| Compliance cluster (audit + regulator reports) | 2 (cohort-4 substrate already shipped) | 2 (10-15h) | 1 (incremental on cohort-4) | 1 (specialized) | 6/12 | Better fit as cohort-7+ |
| Tenant Self-Service (magic-link portal) | 0 (W#20 phases 4-9 pending) | 1 (15-25h) | 2 (customer-touch) | 2 (multi-actor) | 5/12 | Blocked on WS-E |
| Mobile-first PWA (V5 #7) | 3 (pure FED+PAO) | 2 (10-15h) | 2 (customer-touch) | 2 (positioning) | 9/12 | Strong candidate for cohort-6 |
| ERPNext route deletion (V5 #6) | 3 (cleanup) | 3 (4-6h) | 1 (close-out) | 1 (hygiene) | 8/12 | Bundle into cohort-N close-out, not anchor |

---

## 2. Cohort-5 recommended scope — Property Management cluster + AP Aging

### 2.1 Layer 1: Property Management cluster

#### 2.1.1 Substrate dependencies

**Already shipped:**
- `blocks-property-management` cluster (W#17/W#18 — Properties + Vendors all `built`)
- `blocks-public-listings` (W#28 — Anonymous → Prospect → Applicant fully wired; `built`)
- Bridge endpoint family at `signal-bridge/Sunfish.Bridge/Property/` (cohort-1 PRs MERGED)
- Frontend page primitives: `PropertiesPage.tsx`, `LeasesPage.tsx` etc. (cohort-1 MERGED;
  cohort-2 retrofit MERGED)

**Required (small additions):**
- Vacancy admin endpoint family (likely already in cohort-1 listings substrate; verify)
- Unit detail page route (currently 404 per cohort-3 INDEX Q5; deliberately reserved)

#### 2.1.2 Proposed PR shape

| PR | Subject | Effort | Pattern claims |
|---|---|---|---|
| PR 1 | Vacancy admin page (`/vacancies` or `/admin/vacancies`) — list, filter, list/delist actions | 2-3h FED | pattern-009 (rebind) |
| PR 2 | Unit detail page (`/units/{id}`) — replaces 404; full unit lifecycle view | 2-3h FED | pattern-009 |
| PR 3 | (Optional) Unit list page or Property → Units cross-link enhancement | 1-2h FED | pattern-009 |
| PR 4 | Close-out (smoke + docs + ledger flip) | 0.5h | (none) |

Net Property Mgmt scope: 5.5-8.5h FED. Engineer prereq: confirm-or-extend Bridge endpoints
(~2-3h depending on what's already shipped).

#### 2.1.3 Pattern claims

- `@standing-pattern: pattern-009` on PR 1, 2, 3 (Bridge endpoint + frontend rebind pair)
- No new candidate patterns anticipated

### 2.2 Layer 2: AP Aging — tail-end bundled

#### 2.2.1 Substrate dependencies

Per V5 #1b (cohort-6 survey) — substrate gap is `ApAgingSummaryCartridge` mirror of
`ArAgingSummaryCartridge` (~3-4h Engineer). Cartridge consumption pattern proven via
cohort-3 (assuming cohort-3 ships cleanly).

#### 2.2.2 Proposed PR shape

| PR | Subject | Effort | Pattern claims |
|---|---|---|---|
| PR 5 (Engineer) | `ApAgingSummaryCartridge` ship | 3-4h Engineer | (substrate) |
| PR 6 (FED) | Extend `api/reports.ts` with `runApAgingSummary()` + AP Aging page | 2-3h FED | pattern-009 (rebind), pattern-015 (provisionality 2nd cohort), pattern-016 (run-on-demand 2nd cohort), pattern-017 (CSV export 2nd cohort), pattern-013 (cartridge-read-via-post 2nd cohort) |
| PR 7 | Close-out (W#80 ledger flip; doc updates) | 0.5h | (none) |

Net AP Aging tail-end: 5.5-7.5h.

#### 2.2.3 Why bundle into cohort-5 (rather than wait for cohort-6 per V5 #1b)

1. **Ratification efficiency.** Cohort-5 bundling ratifies pattern-013, 015, 016, 017 on a
   single cohort-pass. Cohort-6-deferred AP Aging would either delay these ratifications by
   ~1 week OR force a separate single-PR mini-cohort.
2. **Cohort-3 closure dependency reduction.** AP Aging's cartridge consumption pattern
   depends on cohort-3 closure. If we wait for cohort-6, we add a serialization point.
   Bundling shortens the dependency chain.
3. **Effort fit.** 5.5-7.5h slots cleanly behind the 5.5-8.5h Property Mgmt cluster as a
   sequential second layer. Engineer can ship `ApAgingSummaryCartridge` in parallel with
   FED's Property Mgmt PR 1+2.

### 2.3 Total cohort-5 effort

| Layer | Subject | Effort |
|---|---|---|
| 1 | Property Mgmt cluster (3-4 PRs) | 5.5-8.5h FED + 2-3h Engineer |
| 2 | AP Aging (2 PRs) | 2-3h FED + 3-4h Engineer |
| **Total** | **5-7 PRs across cluster** | **~13-19h** |

### 2.4 Cohort-5 council requirements

| PR | sec-eng SPOT-CHECK | .NET-architect SPOT-CHECK |
|---|---|---|
| PR 1 Vacancy admin | YES (pattern-009 mandatory) | YES (pattern-009 mandatory) |
| PR 2 Unit detail | YES (new route) | NO (mechanical mirror) |
| PR 3 Unit list (optional) | NO (mechanical) | NO |
| PR 4 close-out | NO | NO |
| PR 5 Engineer cartridge | ADVISORY (mechanical mirror per V5 #1b) | ADVISORY |
| PR 6 FED AP Aging | NO (pattern-009 mechanical mirror per cohort-3 2nd-instance) | NO |
| PR 7 close-out | NO | NO |

Net SPOT-CHECK load: 1-2 sec-eng + 1 .NET-architect — comparable to cohort-3's load.

---

## 3. Why ARR/MRR demotes (refresh of V5 #1a)

The V5 #1a recommendation was sound at the time. Four changes since 2026-05-21 inform the
refresh:

1. **Cohort-3 substrate readiness has held but cohort-3 has not shipped.** ARR/MRR's
   substrate dependency on `blocks-subscriptions` accumulator pre-supposes a Stage-05 plan
   that's not yet authored. Property Mgmt's substrate is fully shipped; cohort-5 ship
   timing risk is lower.

2. **Cohort-4 first-cycle RED → cycle-2 GREEN pattern surfaced the wire-contract
   reconciliation gap (S05-1)** per sec-eng cycle-1 verdict §9 + cycle-2 verdict §7-N3.
   ARR/MRR with a novel substrate primitive (accumulator) introduces exactly the kind of
   cross-repo wire-contract risk that S05-1 closes — but only if the Stage-05 plan
   incorporates S05-1 (the template doesn't yet; see V14 #3). Defer ARR/MRR to a cohort
   where the Stage-05 template has matured.

3. **MVP-demo critical-path re-prioritization.** Per MASTER-PLAN §G-1 done-conditions:
   - "[ ] CIC processes first rent collection cycle end-to-end (React UI → ERPNext → bank
     statement)"
   - "[ ] CIC sends first tenant communication through blocks-crew-comms"
   - "[ ] Accountant performs bank reconciliation from their own Sunfish node"
   - "[ ] CPA accesses year-end Schedule-E data via signal-bridge read-only session"
   - "[ ] Spouse logs into her own Sunfish install"

   None of these MVP-demo conditions name ARR/MRR. Property Mgmt directly supports the
   first condition (rent collection cycle includes property + unit + vacancy admin).

4. **ARR/MRR is investor-positioning, not business-validation.** Per MASTER-PLAN §G-1:
   "Proves the local-first paradigm with a real commercial workload." That's property-mgmt-
   running, not SaaS metrics dashboards.

ARR/MRR remains a strong cohort-6 candidate. Demoting is not deferring indefinitely.

---

## 4. Sequencing graph

```
cohort-3 (reports cluster, in flight) ─┬──────┐
                                        │      │ (cartridge consumption pattern proven)
cohort-4 (audit-trail, closing today)──┘      │
                                               ▼
                                      cohort-5 (Property Mgmt + AP Aging)
                                       │
                                       ├── FED PR 1+2+3 (Property Mgmt; parallel-shippable)
                                       │   ↑
                                       │   (no substrate dep; consumes existing cohort-1 endpoints)
                                       │
                                       └── Engineer PR 5 (ApAgingSummaryCartridge)
                                           │
                                           ▼
                                           FED PR 6 (AP Aging page) — gated on PR 5
                                           │
                                           ▼
                                           PR 7 close-out
                                           │
                                           ▼
                                      cohort-6 (ARR/MRR per V5 #1a) OR cohort-6 (Mobile-first PWA per V5 #7)
```

Cohort-5 PR 1+2+3 (Property Mgmt) can ship in parallel with PR 5 (Engineer cartridge).
PR 6 (AP Aging) gates on PR 5. Net wall-clock if parallelized: ~6-8h elapsed.

---

## 5. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Vacancy admin Bridge endpoints not yet shipped | Low (W#28 listings substrate "built") | Medium (PR 1 blocked) | Engineer pre-flight audit of `signal-bridge/Sunfish.Bridge/Property/`; if gap, add to PR 5 scope |
| AP Aging substrate effort exceeds 3-4h (V5 #1b estimate) | Low (mechanical mirror) | Low (cohort-5 elastic) | Time-box PR 5 at 5h; if overrun, split into PR 5a/5b |
| Cohort-3 doesn't close cleanly (delays cohort-5) | Medium (cohort-3 still pre-Stage-06) | Medium (cohort-5 substrate dep) | Cohort-3 first-PR (Trial Balance) is small; ship + observe; cohort-5 PR 1+2+3 don't depend on cohort-3 (Property Mgmt is cohort-1-derived) — only PR 6 does |
| Cohort-4 cycle-2 cascade doesn't merge before cohort-5 dispatch | Low (GREEN attested 2026-05-25T13:12Z) | Low | Confirm CI green + auto-merge before cohort-5 directive |
| Pattern-015 Meaning A/B split decision surfaces unexpectedly | Low (Property Mgmt doesn't carry pattern-015) | None for cohort-5 | Not applicable; risk lives in cohort-6 if ARR/MRR ships then |

---

## 6. Open questions for Admiral / CIC

1. **Cohort-5 anchor confirmation/amendment.** ONR recommends Property Mgmt + AP Aging
   bundle (11/12). Primary alternative: ARR/MRR (7/12 in refresh; was V5 #1a's pick).
   Secondary: AP Aging standalone (10/12). Decision lever: "business-validation priority"
   vs "investor-positioning priority."

2. **WS-D Payments disposition.** MASTER-PLAN marks WS-D as "LIKELY SUPERSEDED at Sunfish
   layer" pending CIC decision after W#60 P3 ships. Until that decision, Payments cluster
   is blocked. If CIC wants to make the call now, Payments could re-enter the cohort-N
   queue.

3. **AP Aging cohort assignment.** V5 #1b recommended cohort-6 standalone. V14 refresh
   recommends cohort-5 bundled. CIC ruling needed if either path is preferred.

4. **Cohort-5 vs cohort-6 ARR/MRR.** If ARR/MRR demotes per this refresh, does CIC want it
   firmly slotted as cohort-6 (defaulting V5 #1a), or kept floating for later
   reconsideration?

---

## 7. Forward-watch for cohort-6+

If cohort-5 = Property Mgmt + AP Aging, then cohort-6 candidates re-rank as:

| Candidate | Score (post-cohort-5) | Notes |
|---|---|---|
| ARR/MRR reporting wave | 9/12 (substrate gap remains but Stage-05 template has matured) | Recommended cohort-6 anchor |
| Mobile-first PWA | 9/12 (pure FED + PAO; substrate ready) | Strong alternative |
| Tenant Self-Service (W#20 ph 4-9) | dep on WS-E ratify | Sequence per Admiral |
| Compliance cluster (audit log surfaces v2) | 7/12 | Incremental on cohort-4 |
| ERPNext route deletion finale | 8/12 (bundle into cohort-N close-out) | Cohort-N close-out, not anchor |

---

## 8. Sources cited

1. ONR V5 #1a `cohort-5-scope-survey-2026-05-21.md` (shipyard PR #95 MERGED) — the survey
   this V14 refresh supersedes
2. ONR V5 #1b `cohort-6-scope-survey-2026-05-21.md` (shipyard PR #96 MERGED) — AP Aging
   cohort-6 default proposal
3. `shipyard/icm/_state/MASTER-PLAN.md` (G-1 done-conditions; current cohort positioning)
4. `shipyard/_shared/design/cohort-4/PRE-SCOPE.md` (cohort-4 PRE-SCOPE for AP Aging fit
   reference)
5. `shipyard/icm/_state/active-workstreams.md` (W#17, W#18, W#28, W#60 status; substrate
   readiness signals)
6. `coordination/inbox/council-verdict-2026-05-25T1312Z-security-engineering-sunfish-71-cycle-2-reattest.md` (cohort-4 GREEN attest; cohort-5 unblock signal)
7. `coordination/inbox/council-verdict-2026-05-22T1611Z-security-engineering-sunfish-71-cycle-1-reattest.md` (cohort-4 cycle-1 AMBER; S05-1/-2/-3 surfacings)
8. V5 #7 mobile-first UX (shipyard PR #94) — alternative candidate context
9. V5 #6 ERPNext migration (shipyard PR #93) — ERPNext disposition context
10. MASTER-PLAN.md §Phase 2 (WS-D Payments disposition; WS-E Tenant Self-Service blockers)

---

## 9. What ONR does next

V14 #2 deliverable complete. Files `onr-status-2026-05-25T1530Z-v14-2-cohort-5-scope-
survey-complete.md` naming this deliverable path. Proceeds to V14 #3 (Stage-05 protocol
amendments post-cohort-4).

— ONR, 2026-05-25T15:30Z
