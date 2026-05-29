# ONR research — Cohort-4 scope survey (2026-05-21)

**Requester:** Admiral (per `admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #6)
**Authored by:** ONR
**Authored at:** 2026-05-21T12-32Z
**Status:** draft (CIC ratification on cohort-4 anchor candidate selection)

---

## Scope of investigation

- **In scope:** survey candidate scopes for cohort-4 (next anchor cohort after cohort-3 reports cluster ships). Per V2 directive #6 framing: "Which substrate primitives are still missing for each candidate? Which cohort would unblock most ERP MVP demos?" + prioritized candidate list + dependencies.
- **Out of scope:** authoring the cohort-4 Stage-06 hand-off (separate work item once CIC ratifies the anchor candidate); ERPNext deprecation timing (Cohort 4 RB-12 close-out is already named as part of the rebind initiative; cohort-4 anchor scope is broader).
- **Authoritative sources consulted:** V2 directive item #6 candidate list (6 candidates); MASTER-PLAN.md (referenced by W#60 P4 hand-off); cohort-3 hand-off (shipyard#51) §1.3 forward-watch ("Cohort-4 candidates" — ApAgingPage + ERPNext route deletion); V2 #4 research recommending JournalEntry POST as pattern-010 anchor (cross-cohort convergence point).
- **Success looks like:** Admiral has 6 candidates ranked by dependency-readiness + MVP-demo-unblock-value + ONR provisional recommendation; CIC ratifies anchor; cohort-4 hand-off authored as separate workstream.

---

## TL;DR

1. **Six candidates per V2 directive #6:**
   - **C1 — ARR/MRR reporting wave** (financial summaries; investor-grade)
   - **C2 — Multi-tenant administration surface** (super-admin UI; tenant CRUD)
   - **C3 — Audit-trail viewer** (consume Bridge audit emission retrofit from V2 #3)
   - **C4 — ERPNext migration tools** (`IErpnextJournalEntryImporter` substrate)
   - **C5 — Mobile-first UX wave** (Surface Pro + iPad form factors)
   - **C6 — Real-time collaboration features** (SignalR; per V3 #5 audit)

2. **Plus implicit candidates from cohort-3 forward-watch:**
   - **C7 — AP Aging cartridge + page** (carried from cohort-3 deferral; gated on `ApAgingSummaryCartridge` shipping)
   - **C8 — ERPNext route deletion (RB-12)** (close-out of the rebind initiative)

3. **ONR provisional ranking (anchor-candidacy descending):**
   - **#1 C3 Audit-trail viewer** — substrate already ready (V2 #3 retrofit; audit substrate per ADR 0049); high MVP-demo unblock value (compliance story); ~6-8h
   - **#2 C7 AP Aging page** — substrate gap is ApAgingSummaryCartridge (Engineer ship; ~3-4h cartridge + ~2-3h FED page); completes the reports cluster narrative
   - **#3 C1 ARR/MRR reporting wave** — substrate gap is subscription-event accumulation (`blocks-subscriptions` extension); higher business value but bigger surface
   - **#4 C5 Mobile-first UX wave** — pure FED + design; high customer-touch value; needs PAO Track C cohort-4 direction
   - **#5 C2 Multi-tenant admin surface** — needs CIC ratification on super-admin model + claims-backed auth (production OIDC ADR pre-stages it); high effort
   - **#6 C8 ERPNext route deletion** — close-out PR; gated on all rebind cohorts complete
   - **#7 C4 ERPNext migration tools** — substrate-heavy; mature when first customer migrates from ERPNext
   - **#8 C6 Real-time collaboration** — SignalR substrate not yet present; longest dependency chain

4. **ONR recommends C3 (Audit-trail viewer) as cohort-4 anchor.** Substrate ready; MVP-demo unblock value high; ~6-8h Engineer-side; FED-light; convergent with V2 #3 retrofit shipping (forensics visibility becomes user-visible immediately).

5. **W#60 P4 PR 2 (Accountant journal-entry POST) + pattern-010 ratification (per V2 #4) may converge with cohort-4 anchor** — if CIC anchors cohort-4 on JournalEntry POST, pattern-010 ratifies via cohort-4 + W#60 P4 PR 2 simultaneously.

---

## 1. Candidate matrix

| # | Candidate | Substrate readiness | MVP-demo unblock value | Effort | Dependencies | ONR rank |
|---|---|---|---|---|---|---|
| C1 | ARR/MRR reporting wave | Partial — `blocks-subscriptions` accumulator missing | High (investor-grade) | 12-20h | blocks-subscriptions extension | 3 |
| C2 | Multi-tenant admin surface | Partial — production OIDC pre-staged (V1 #5); claims-backed auth needed | Medium (operator-facing) | 15-25h | ADR for super-admin role; production OIDC | 5 |
| C3 | Audit-trail viewer | **READY** — V2 #3 retrofit + audit substrate (ADR 0049) | **High (compliance + forensics)** | 6-8h | V2 #3 retrofit PR landed | **1** |
| C4 | ERPNext migration tools | Substrate missing — `IErpnextJournalEntryImporter` not yet present | Low (customer-trigger; not MVP-blocking) | 20-30h | New substrate package | 7 |
| C5 | Mobile-first UX wave | Pure FED + PAO; substrate is browser features (responsive CSS; touch targets) | Medium (customer-touch) | 10-15h | PAO Track C cohort-4 design | 4 |
| C6 | Real-time collaboration (SignalR) | Substrate missing — SignalR hub + WebSocket infrastructure | Low-medium (post-MVP polish) | 25-40h | New ADR for real-time substrate | 8 |
| C7 | AP Aging page | Substrate gap — `ApAgingSummaryCartridge` (3-4h Engineer); FED page (~2-3h) | Medium (completes reports cluster) | 5-7h total | Engineer cartridge ship | 2 |
| C8 | ERPNext route deletion (RB-12) | Substrate ready — all rebind cohorts close-out | Low (cleanup) | 4-6h | All rebind cohorts complete | 6 |

---

## 2. ONR provisional recommendation: C3 — Audit-trail viewer

### 2.1 Why C3 wins anchor

- **Substrate-ready post-V2 #3.** Once Engineer ships the audit-emission retrofit PR (per V2 #3 research), the audit substrate produces high-quality forensics data. Cohort-4 anchor consumes that data via UI.
- **High MVP-demo unblock value.** Compliance story: "I can see every cross-tenant probe attempt + every tenant action, with cryptographically signed payloads." Investor demos + customer compliance demos both gain a strong artifact.
- **Effort-light.** ~6-8h Engineer-side (1-2 Bridge endpoints + 1 FED page); no substrate work.
- **No new dependencies beyond V2 #3 retrofit.** Pattern-009 frontend rebind pair applies; cohort-1/cohort-2 precedents transfer.
- **Convergence with W#60 P4 PR 2 + pattern-010.** Audit-trail viewer consumes JournalEntry events from W#60 P4 PR 2 + audit events from V2 #3 retrofit. Pattern-010 ratification (V2 #4 recommendation) lands in W#60 P4 PR 2; cohort-4 audit-trail viewer can include a JournalEntry detail page that exercises the pattern-010 invariants.

### 2.2 Cohort-4 (audit-trail anchor) PR cluster shape

| PR | Subject | Owner | Effort |
|---|---|---|---|
| Engineer PR 0 | Bridge endpoint family `GET /api/v1/audit-trail` (query params: from, to, eventType, tenantId-server-derived) + audit search service | Engineer | 2-3h |
| FED PR 1 | `apps/web/src/api/audit-trail.ts` shared client + `AuditTrailPage.tsx` (table view; filter controls; CSV export) | FED | 2-3h |
| FED PR 2 | `AuditEventDetailPage.tsx` (single-event detail; signature verification badge; payload pretty-print) | FED | 1-2h |
| FED PR 3 | Close-out (ERPNext mark + ledger flip) | FED | 1h |

**Total:** ~6-9h across 4 PRs.

### 2.3 PAO Track C cohort-4 design needs

- `apps/docs/blocks/audit-trail/` page outline
- `AuditTrailPage` table layout (rows + filter sidebar)
- `AuditEventDetailPage` payload pretty-print (JSON tree view; signature verification badge state machine: Verified / Verification Failed / Not Signed)
- Color palette for event severity (TenantBoundaryViolation = red; PaymentRecorded = blue; etc.)

---

## 3. Alternate anchors (if CIC prefers different scope)

### 3.1 If CIC anchors on C7 (AP Aging)

- Closes the reports cluster narrative (cohort-3 left AP Aging deferred)
- Engineer cartridge work: ~3-4h
- FED page work: ~2-3h
- Total: ~5-7h
- Less compliance-story value than C3
- Pattern-009 mechanical mirror; minimal sec-eng surface

### 3.2 If CIC anchors on C1 (ARR/MRR)

- Highest business value (investor-grade)
- Substrate gap: `blocks-subscriptions` accumulator (~6-8h Engineer)
- FED pages: ARR dashboard + MRR detail + cohort retention chart (~6-12h)
- Total: ~12-20h
- New analytics primitives might generate new pattern candidates (pattern-012-event-accumulator-aggregation?)

### 3.3 If CIC anchors on C5 (Mobile-first UX)

- High customer-touch value (Surface Pro is the current MVP target form factor)
- Pure FED + PAO work; no substrate
- PAO Track C must lead with mobile-first design language
- Effort: ~10-15h FED across responsive sweeps of cohort-1 + cohort-2 + cohort-3 pages
- Lower investor-demo value than C3

---

## 4. Cross-cohort interactions

### 4.1 Cohort-4 and W#60 P4 PR 2 (Accountant role)

W#60 P4 PR 2 ships `POST /api/v1/accounting/journal-entries` (per W#60 P4 hand-off). If cohort-4 anchors on C3 (Audit-trail viewer), it CONSUMES JournalEntry audit events. Convergence: pattern-010 ratifies in W#60 P4 PR 2; cohort-4 audit viewer renders JournalEntry events with pattern-010-compliant rich payload.

### 4.2 Cohort-4 and V2 #3 retrofit

V2 #3 retrofit emits `TenantBoundaryViolation` events. C3 anchor's audit-trail viewer surfaces those events — forensics visibility becomes user-visible. **One PR (cohort-4 C3) closes the loop on V2 #3 retrofit's MVP value.**

### 4.3 Cohort-4 and ADR 0091 Step 3/4 (V2 #1)

Cohort-4 PR 0 (audit endpoint) consumes `Foundation.MultiTenancy.ITenantContext` (server-side tenant derivation) — exercises Step 2.0/2.1 narrowed-interface pattern. No new surface added; just consumes.

### 4.4 Cohort-4 and ADR 0092 Step 2 (V2 #2)

Cohort-4 PR 0 audit-search query uses `HasQueryFilter` for tenant scoping — exercises Step 2 EFCore convention. No new surface added; just consumes.

---

## 5. Forward-watch (NOT cohort-4 scope)

These items emerged during V2 #6 survey but are out-of-scope for cohort-4 anchor; logged for future cohort consideration:

- **C8 ERPNext route deletion (RB-12)** — close-out of the rebind initiative. Schedule after C7 (AP Aging) completes the reports cluster; before C4 (ERPNext migration tools) which is a new substrate.
- **C6 Real-time collaboration (SignalR)** — needs a new ADR for the real-time substrate. ONR proposes scoping a separate ADR after cohort-4 ships (V3 or V4 batch research item).
- **Cross-chart reporting** (per V2 #5 multi-chart research §"Phase 4") — demand-driven; trigger is first multi-chart-tenant onboarding.
- **Refund / void Bridge surface** (cohort-2 forward-watch §7 #11) — semantically risky; defer until refund substrate hardens.
- **Bank CSV ingest** (W#60 P4 PR 4) — under W#60 not cohort-4; ONR's cohort-4 anchor recommendation doesn't conflict.

---

## 6. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

### For CIC

1. **Cohort-4 anchor — C3 Audit-trail viewer (ONR recommended) vs C7 AP Aging (completes reports cluster) vs C1 ARR/MRR (investor value)?** Three viable anchors; ONR ranks C3 first by readiness + demo value.
2. **C7 AP Aging slipped to cohort-4 or split into 4a/4b** — does cohort-4 carry AP Aging as a follow-on PR (after the anchor work) OR does AP Aging anchor its own cohort?
3. **Pattern-010 ratification path** — converge in W#60 P4 PR 2 + cohort-4 audit viewer (ONR recommended) OR designate a separate ratification cohort?

### For Admiral

1. **PAO Track C cohort-4 design routing** — needed BEFORE FED PRs in cohort-4. Trigger PAO dispatch when CIC ratifies anchor.
2. **Cohort-4 PR-count cap** — cohort-1=4, cohort-2=8, cohort-3=5 (4 FED + Engineer prereq); cohort-4 estimated 4-7 depending on anchor.

### For .NET-architect council

1. **Audit search performance** — `GET /api/v1/audit-trail?from=...&to=...&eventType=...` requires indexed audit table; cohort-4 PR 0 acceptance criteria includes EXPLAIN-plan verification?

---

## 7. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #6 — 6-candidate list
2. `shipyard/icm/_state/handoffs/anchor-react-rebind-cohort-3-stage06-handoff.md` §1.3 — AP Aging deferral; ERPNext deletion RB-12
3. V2 #3 research (audit-emission Bridge retrofit) — substrate readiness for C3
4. V2 #4 research (pattern-010 3rd-instance) — pattern-010 ratification path
5. V2 #5 research (multi-chart) — cross-cohort interaction
6. ADR 0049 (audit substrate) — IAuditTrail + AuditRecord
7. W#60 P4 hand-off PR 2 — Accountant journal-entry POST surface
8. Cohort-1 + cohort-2 + cohort-3 hand-offs (W#74/W#76/W#77) — PR-cluster shape precedent

---

## 8. What ONR does next

Returns to V2 research queue. Per proceed-continuously discipline:

- Item #6 deliverable complete (this doc + status beacon).
- File `onr-status-*-research-queue-v2-item-6-cohort-4-scope-survey-complete.md`.
- Proceed to V2 #7: WS-E messaging substrate Phase 10+ addendum (~3-4h; final V2 item).

— ONR, 2026-05-21T12:32Z
