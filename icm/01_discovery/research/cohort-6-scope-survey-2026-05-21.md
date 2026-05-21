# Cohort-6 scope survey (2026-05-21)

**Authored by:** ONR (V5 batch item #1b)
**Requester:** Admiral (per `admiral-directive-2026-05-21T14-30Z` item #1)
**Authored at:** 2026-05-21T14-55Z

---

## Scope

Companion to V5 #1a cohort-5 survey (shipyard#95). What's cohort-6 anchor after cohort-5 closes?

---

## TL;DR

1. **ONR recommends cohort-6 anchor = C7 AP Aging page** (8/12 candidate score; closes reports cluster narrative; substrate gap is small).

2. **PR cluster shape:** Engineer prereq PR 0 (`ApAgingSummaryCartridge` ship; ~3-4h) + 2 FED PRs (`api/reports.ts` extension + `ApAgingPage.tsx` new) + close-out PR 3 = 4 PRs.

3. **Effort estimate:** ~5-7h total (~3-4h Engineer cartridge + ~2-3h FED page).

4. **Substrate gap:** `ApAgingSummaryCartridge` (cartridge package at `shipyard/packages/blocks-reports/Cartridges/ApAgingSummary/`). Reserved enum stub exists per V1 #1 cohort-3 hand-off; cartridge not yet shipped.

5. **Pattern claims:** pattern-009 formal + reuses cohort-3 cartridge consumption pattern.

6. **Sequencing dependency:** cohort-6 depends on (a) cohort-3 reports cluster (W#77) shipped — needs cartridge consumption pattern established; (b) `ApAgingSummaryCartridge` ship by Engineer (~3-4h prereq).

7. **MVP-demo unblock value: MEDIUM** — closes the reports cluster cleanly; investor demo gets a complete AR + AP aging story.

---

## 1. Cohort-6 anchor scoring (post-cohort-5 = C1 ARR/MRR)

If cohort-5 ships C1 (ARR/MRR reporting per V5 #1a recommendation), cohort-6 candidates re-rank:

| Candidate | Substrate readiness | Effort | MVP-demo value | Dependencies | Score |
|---|---|---|---|---|---|
| **C7 — AP Aging page (cohort-3 deferred)** | `ApAgingSummaryCartridge` (~3-4h Engineer) | 5-7h | Medium-high (closes reports) | Engineer cartridge + cohort-3 W#77 shipped | **8/12** |
| C5 — Mobile-first UX PWA | Pure FED + PAO | 10-15h | Medium (customer-touch) | PAO Track C cohort-6 design | 7/12 |
| C2 — Multi-tenant admin | Production OIDC needed | 15-25h | Medium (operator-facing) | ADR for super-admin + production OIDC ADR (V1 #5 future) | 6/12 |
| C8 — ERPNext route deletion (RB-12) | Substrate ready | 4-6h | Low (close-out cleanup) | All cohort-N rebind PRs shipped | 5/12 |
| C4 — ERPNext migration | Partial | 25-40h | Low (demand-driven) | Customer signal | 5/12 |
| C6 — Real-time collab | Substrate missing | 25-40h | Low (polish) | New ADR | 4/12 |

---

## 2. AP Aging page — proposed cohort-6 anchor

### 2.1 Substrate dependencies

**Required (Engineer prereq):**
- `ApAgingSummaryCartridge` (cartridge package at `shipyard/packages/blocks-reports/Cartridges/ApAgingSummary/`)
- Mirrors existing `ArAgingSummaryCartridge` shape (per V1 #1 cohort-3 hand-off context)
- Adds enum case to `ReportKind` (reserved per FED scope survey 2026-05-19T12:00Z; just needs cartridge impl)

**Already shipped:**
- W#72 blocks-reports cartridge runner + 4 Phase 1 cartridges (Trial Balance + AR Aging + P&L by Property + Rent Roll)
- Cohort-3 cartridge consumption pattern (5 FED PRs from cohort-3 W#77; pattern-009 formal + pattern-013-cartridge-read-via-post candidate)
- Cohort-3 cartridge-runner Bridge endpoint family at `signal-bridge/Sunfish.Bridge/Reports/` (per cohort-3 Engineer prereq PR 0)

### 2.2 Engineer scope (~3-4h)

| Subject | Effort |
|---|---|
| `ApAgingSummaryCartridge` implementation (mirror ArAgingSummary) | 2-3h |
| Cartridge tests (per-tenant + per-vendor breakdown + aging buckets) | 1h |

### 2.3 FED scope (~2-3h)

| PR | Subject | Effort |
|---|---|---|
| PR 1 | Extend `sunfish/apps/web/src/api/reports.ts` with `runApAgingSummary()` + types | 0.5h |
| PR 2 | `ApAgingPage.tsx` (mirrors ArAgingPage from cohort-3 PR 5; ByVendor + ByProperty tabs + TopDelinquent vendor list) | 2-3h |
| PR 3 | Close-out (docs running log + E2E smoke extension + W#80 ledger flip) | 0.5h |

### 2.4 Pattern claims

- `@standing-pattern: pattern-009` (formal; cluster-endpoint rebind pair)
- `@candidate-pattern: pattern-013-cartridge-read-via-post` (2nd instance after cohort-3 PR 1; pattern ratification trigger)

### 2.5 Sec-eng + .NET-architect council requirements

- Engineer prereq PR 0: advisory only (mechanical mirror of ArAgingSummary cartridge)
- FED PRs: NOT required (pattern-009 mechanical mirror)

---

## 3. Sequencing within cohort-6

```
Engineer PR 0 (ApAgingSummaryCartridge) →
FED PR 1 (api/reports.ts extension) →
FED PR 2 (ApAgingPage.tsx) →
FED PR 3 (close-out)
```

Sequential. Total ~5-7h.

---

## 4. Alternative cohort-6 anchor: C5 Mobile-first PWA (7/12)

If CIC prefers customer-touch over reports-completeness:

- Path A PWA per V5 #7 (~10-15h FED + ~2h Engineer)
- Higher engineering cost
- Higher customer-touch value
- See V5 #7 (shipyard#94) for full scope

---

## 5. Cohort-7+ candidates (after cohort-5 + cohort-6)

Post-cohort-5 (C1 ARR/MRR) + cohort-6 (C7 AP Aging OR C5 Mobile PWA):

- **Cohort-7:** the C5/C7 anchor NOT chosen at cohort-6
- **Cohort-8:** C8 ERPNext route deletion RB-12 (close-out of the rebind initiative)
- **Cohort-9+:** demand-driven candidates (C4 ERPNext migration; C2 multi-tenant admin); pre-staged ADR work

---

## 6. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| `ApAgingSummaryCartridge` Engineer effort exceeds estimate | Low | Low | Mirror of ArAgingSummary; well-understood pattern |
| Cohort-3 cartridge consumption pattern hasn't proven out | Medium | Medium | Wait for cohort-3 PR 5 (AR Aging) to merge + smoke-test before cohort-6 dispatch |
| Pattern-013-cartridge-read-via-post 2nd instance triggers ratification BEFORE catalog amendment lands | Low | Low | V5 #2 catalog hygiene (shipyard#88) already added pattern-013 as candidate; ratification trigger documented |

---

## 7. Open questions for Admiral routing

1. **Cohort-6 anchor confirm/amend** — ONR recommends C7 AP Aging (8/12); alternative C5 Mobile-first PWA (7/12)
2. **Cohort-6 vs C7-defer-to-cohort-7** — if cohort-5 anchors C1 ARR/MRR, does CIC prefer cohort-6 = C7 (closes reports) OR C5 (customer-touch)?
3. **Cohort-3 reports cluster shipping precondition** — cohort-6 requires cohort-3 closed (W#77 built); confirm sequencing acceptable

---

## 8. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T14-30Z` item #1
2. V5 #1a cohort-5 scope survey (shipyard#95) — companion + cohort-5 ranking
3. V5 #7 mobile-first UX (shipyard#94) — alternative C5 candidate
4. V5 #6 ERPNext migration (shipyard#93) — alternative C4 candidate
5. V1 #1 cohort-3 hand-off (shipyard#51) — AP Aging deferral + cartridge consumption pattern
6. V5 #2 pattern catalog hygiene (shipyard#88) — pattern-013-cartridge-read-via-post candidate entry

---

## 9. What ONR does next

V5 #1b deliverable complete. V5 batch CLEARED (8/8). Files V5-cleared idle beacon per directive § "V5 complete → idle beacon → V6 dispatch."

— ONR, 2026-05-21T14:55Z
