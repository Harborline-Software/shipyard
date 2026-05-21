# ERPNext migration tools — cohort candidate scoping (2026-05-21)

**Authored by:** ONR (V5 batch item #6)
**Requester:** Admiral (per `admiral-directive-2026-05-21T14-30Z` item #6)
**Authored at:** 2026-05-21T14-45Z

---

## Scope

V2 #6 cohort-4 scope survey listed "ERPNext migration tools" (candidate C4). Now survey what ERPNext migration would actually entail to inform cohort-5 + cohort-6 anchor selection.

---

## TL;DR

1. **ERPNext migration is a HIGH-EFFORT cohort.** ~25-40h Engineer + FED across substrate work + migration tooling + reconciliation UI. NOT a quick win.

2. **Two distinct scopes within "ERPNext migration":**
   - **Scope A — Full data import (one-time):** customer migrates ERPNext data → Sunfish at onboarding. Customer-trigger; only fires when a customer has prior ERPNext data.
   - **Scope B — Incremental sync (ongoing):** Sunfish stays bidirectionally synced with ERPNext during a coexistence window. Higher complexity; substrate-heavy.

3. **ONR recommends Scope A for cohort-N+1+ candidacy; Scope B as future workstream.** Scope A unblocks specific customers; Scope B is engineering polish.

4. **Substrate readiness:** `IErpnextJournalEntryImporter` exists as scaffolding per memory; `ERPNextProxy.cs` (legacy /api/v1/erpnext/* routes) is being deprecated cohort-by-cohort (RB-12 close-out). Migration tools build on the proxy + add reconciliation primitives.

5. **NOT cohort-5 candidacy unless customer signal:** ERPNext migration is demand-driven (similar to multi-chart per V2 #5). Cohort-5 anchor should prioritize MVP-demo-unblock or substrate-cleanup over demand-driven candidates.

6. **Cohort-N+1+ candidacy framing:** when ERPNext migration activation gate fires (per pattern matching V3 #5 multi-chart gate), file `cic-question-*-erpnext-migration-activation.md` + dispatch.

---

## 1. Migration scope dimensions

### 1.1 Scope A — Full data import (one-time)

**Trigger:** customer has historical ERPNext data they want to keep when migrating to Sunfish.

**Data to import:**
- Chart of Accounts → `IChartCatalogService` + `IJournalStore` entries
- Customers → `blocks-leases.Party` records
- Vendors → `blocks-financial-ap.Vendor` records
- Open Invoices → `blocks-financial-ar.Invoice` records (status preserved)
- Paid/Closed Invoices → audit-only persistence (read-only historical)
- Payment History → `blocks-financial-payments.Payment` records
- Bills (AP) → `blocks-financial-ap.Bill` records
- Bank statements → reconciliation queue
- Reports historical → optional; reports cluster (W#72) regenerates on demand

**Out of scope (cannot import or shouldn't):**
- ERPNext customizations (Frappe doctype modifications)
- ERPNext workflows (Sunfish's workflow surface differs)
- ERPNext file attachments (separate import pipeline; out of scope)
- Real-time ERPNext integrations (Sunfish doesn't replicate these)

### 1.2 Scope B — Incremental sync (ongoing)

**Trigger:** customer wants Sunfish + ERPNext to coexist for some window (transition period; dual-reporting compliance).

**Sync directions:**
- ERPNext → Sunfish: read-side mirror (audit + reporting + drill-down from Sunfish UI)
- Sunfish → ERPNext: write-side mirror (Sunfish journal entries reflect into ERPNext for legacy consumers)

**Complexity:**
- Bi-directional conflict resolution (which side wins on simultaneous edits?)
- Field mapping (Sunfish + ERPNext schemas differ on extension fields)
- Operational overhead (sync daemon; failure recovery; ops dashboards)

**ONR's read:** Scope B is substantial. Estimated ~3-6 months Engineer + FED + operational. Not cohort-scope; SEPARATE WORKSTREAM if customer demands.

---

## 2. Scope A — implementation surface

### 2.1 Engineer scope (~20-30h)

| Component | Effort | Notes |
|---|---|---|
| `IErpnextJournalEntryImporter` (verify shipped) | 0h (assumed shipped per memory) | Substrate baseline |
| `IErpnextDataImporter` (new) | 4-6h | Per-doctype import (Customer, Vendor, Invoice, Bill, Payment) |
| Field-mapping config (Sunfish ↔ ERPNext schema) | 3-4h | JSON config + per-tenant override |
| `ErpnextImportSession` entity + persistence | 2-3h | Tracks import state; rollback on partial failure |
| Bridge endpoint `POST /api/v1/erpnext-import` | 2-3h | Long-running job; returns session ID + status URL |
| Bridge endpoint `GET /api/v1/erpnext-import/{sessionId}` | 1h | Poll session state |
| Reconciliation primitive | 3-5h | Compare ERPNext vs Sunfish state post-import; produce delta report |
| Tests (~15-20 integration tests) | 5-7h | Per-doctype import + rollback + reconciliation |

### 2.2 FED scope (~5-10h)

| Component | Effort | Notes |
|---|---|---|
| Import wizard UI (file upload + per-doctype mapping) | 3-4h | Multi-step form; ERPNext export file → field mapping → confirm |
| Import progress dashboard | 2-3h | Real-time progress; per-doctype counts; error log |
| Reconciliation viewer | 2-3h | Sunfish-side post-import diff vs ERPNext snapshot |

### 2.3 Total Scope A effort

~25-40h Engineer + FED across 4-6 PRs.

---

## 3. Activation gate (when does ERPNext migration ship?)

Mirroring V3 #5 multi-chart activation gate pattern:

### Trigger A — Customer onboarding with existing ERPNext data

**Condition:** Customer signs up with explicit ERPNext history (>3 months of data they want preserved).

**Detection:** sales call note / onboarding questionnaire mentioning "current accountant uses ERPNext" / "moving from Frappe" / "have ERPNext exports".

### Trigger B — Investor demo includes "ERPNext escape hatch"

**Condition:** Investor pre-meeting names ERPNext-data preservation as deal-breaker.

### What does NOT fire the gate

- Internal Sunfish engineering aesthetics
- Cohort-4 (audit-trail viewer) closing — unrelated
- Generic "migration support" as a positioning lever — wait for concrete customer signal

---

## 4. Cohort-5/6 candidacy assessment

### Cohort-5 anchor scoring

| Anchor | Substrate gap | Effort | MVP-demo value | Dependencies | Score |
|---|---|---|---|---|---|
| C1 ARR/MRR reporting | `blocks-subscriptions` accumulator | 12-20h | High (investor) | None | **9/12** |
| C7 AP Aging page (cohort-3 deferred) | `ApAgingSummaryCartridge` (~3-4h Engineer) | 5-7h | Medium (closes reports) | Cohort-3 reports cluster on main | **8/12** |
| **C4 ERPNext migration (this research)** | Partial — `IErpnextJournalEntryImporter` shipped; rest not | **25-40h** | Low (demand-driven; not MVP-blocking) | Customer signal | **5/12** |
| C5 Mobile-first UX | Pure FED + PAO | 10-15h | Medium (customer-touch) | PAO Track C cohort-5 design | **7/12** |
| C2 Multi-tenant admin | Production OIDC needed | 15-25h | Medium (operator-facing) | ADR 0093+ OIDC ADR | **6/12** |
| C6 Real-time collaboration (SignalR) | Substrate missing | 25-40h | Low (polish) | New ADR | **4/12** |

**ONR ranking for cohort-5 (per V5 #1 input):**
1. C1 ARR/MRR reporting (9/12; highest investor value; no demand-trigger)
2. C7 AP Aging page (8/12; closes reports narrative; substrate gap is small)
3. C5 Mobile-first UX (7/12; high customer-touch; FED-pure)

**ERPNext migration C4 scores 5/12** — demand-driven candidate; NOT cohort-5 anchor unless customer signal fires.

### Cohort-6 anchor scoring (post-cohort-5)

Depends on what cohort-5 chose. If cohort-5 = C1:
1. C7 AP Aging (still 8/12; closes reports)
2. C5 Mobile-first UX (7/12)
3. C2 Multi-tenant admin (6/12)
4. C4 ERPNext migration (5/12; demand-driven; still pending signal)

**ERPNext migration could be cohort-6 if a customer signal fires by cohort-6 dispatch time.**

---

## 5. Open questions for CIC (via Admiral routing)

1. **ERPNext migration activation gate confirm/amend** — 2 triggers proposed (customer onboarding with ERPNext history; investor deal-breaker). Confirm.
2. **Scope A vs Scope B prioritization** — ONR recommends Scope A only (one-time import) for cohort-N+; Scope B (incremental sync) deferred as separate workstream. Confirm.
3. **Cohort-5 anchor ranking** — ONR ranks C1 first; awaiting V5 #1 cohort-5 survey for canonical recommendation.

---

## 6. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T14-30Z` item #6
2. V2 #6 cohort-4 scope survey (shipyard#74) — candidate inventory + scoring framework
3. V3 #5 multi-chart activation gate (shipyard#80) — analogous demand-driven activation pattern
4. `IErpnextJournalEntryImporter` substrate (per memory; not deeply inspected this research)
5. `ERPNextProxy.cs` legacy routes (deprecating cohort-by-cohort per cohort-1/2/3 close-outs)

---

## 7. What ONR does next

V5 #6 deliverable complete. Files `onr-status-*-v5-item-6-erpnext-migration-scoping-complete.md`. Proceeds to V5 #7 (Mobile-first UX architecture).

— ONR, 2026-05-21T14:45Z
