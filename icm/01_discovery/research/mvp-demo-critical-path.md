# MVP demo critical-path analysis

**Authored by:** ONR (V7 batch item #3)
**Requester:** Admiral (per `admiral-directive-2026-05-22T12-45Z` item #3)
**Authored at:** 2026-05-22T13-25Z

---

## Purpose

Identify the **shortest viable path** to a demo-able Sunfish MVP — what must ship, what can be cut, what is precedent-critical vs cosmetic. Risk-weighted timeline.

---

## 1. MVP demo definition (assumed scope)

**Demo audience:** prospective tenant (small property-management firm; ~50-200 units).
**Demo flow (end-to-end):**

1. Tenant onboarding (sign-up + tenant context provisioning)
2. Property + Unit creation
3. Lease creation + tenant link
4. Maintenance request creation + status flow
5. Financial: journal entry, receivables, payables
6. Reports: ARR/MRR + AP Aging snapshot
7. Audit-trail review (admin viewer)

**Non-goals for MVP demo:**
- Multi-tenant federation (signal-bridge cross-tenant federation patterns)
- Field-capture mobile (W#23 iOS)
- Cartridge marketplace
- Anchor desktop (Tauri) — web-only acceptable
- AP/AR aging beyond snapshot (no payment processing flow)

---

## 2. Cohort completion status (2026-05-22 snapshot)

| Cohort | Scope | Stage | Demo-critical? |
|---|---|---|---|
| Cohort-1 (W#74) | Properties + Leases + Maintenance | MERGED (PR #42) | YES (steps 2-4) |
| Cohort-2 (W#76) | Financial cluster (journal, ledger, write-path) | MERGED (PR #58 + ladder) | YES (step 5) |
| Cohort-3 (W#77) | Reports framework | MERGED (PR #51) | YES (step 6 framework only) |
| Cohort-4 (W#78) | Audit-trail viewer | MERGED (PR #81) | YES (step 7) |
| Cohort-5 (W#79?) | ARR/MRR report | SCOPED (V5 #1a 135-line survey) | YES (step 6 content) |
| Cohort-6 (W#80?) | AP Aging report | SCOPED | YES (step 6 content) |
| Onboarding-ladder | Tenant sign-up + context provisioning | NOT YET SCOPED | YES (step 1) |

---

## 3. Critical-path identification

### 3.1 Already shipped (steps 2-5, 7)

Cohort-1 + Cohort-2 + Cohort-4 are in main. Steps 2 (Property/Unit), 3 (Lease), 4 (Maintenance), 5 (Financial), 7 (Audit) work today.

### 3.2 Gap: step 1 (onboarding)

**Status:** UNSCOPED. No tenant-onboarding ladder exists.

**Required:**
- Tenant sign-up form (FED; `apps/web/onboarding/`)
- Tenant context provisioning Bridge endpoint (Engineer; `/admin/tenants/create`)
- ITenantContext seed for new tenants (Engineer; tied to ADR 0091)
- Initial admin user provisioning + auth integration
- Welcome flow to dashboard

**Risk:** **HIGHEST single-cohort risk.** Touches every ADR layer (0091 ITenantContext, 0092 substrate, 0049 audit). Cross-cohort dependency: every other cohort assumes tenant context exists.

**Effort estimate:** 1.5-2 cohort-cycles (cohort-1 + cohort-2 combined complexity); ~7-10 PRs.

### 3.3 Gap: step 6 (reports content)

**Status:** Cohort-3 framework ready; cohort-5 + cohort-6 SCOPED but not built.

**Required:**
- Cohort-5 ARR/MRR Stage-06 build (V5 #1a survey complete; Stage-05 hand-off needed)
- Cohort-6 AP Aging Stage-06 build (V5 #1b survey complete; Stage-05 hand-off needed)

**Effort estimate:** Each ~1 cohort-cycle (~3-4 PRs each). Parallel-buildable.

### 3.4 Other gaps surfaced during analysis

- **Tenant logout + re-auth** — not yet built; needed for any real demo
- **Error-page UX** — anchor-react default 404/500 pages exist but no polish
- **Empty-state UX** — when no properties/leases exist, what does the UI show?
- **Demo data seed script** — for demo runs, want canonical seed (3 properties, 8 units, 5 leases, etc.)

---

## 4. Shortest viable path (critical path)

**Demo-able MVP path = onboarding-ladder + cohort-5 + cohort-6 + demo-polish.**

Sequence (parallel where possible):

```
Track A (Onboarding) ──────────────────────────────►  [SHIPPED]
  ↓
  ├── A1: Tenant context provisioning (Engineer)        ~2 PRs
  ├── A2: Tenant sign-up form (FED)                      ~2 PRs
  ├── A3: Auth integration + admin user seed             ~2 PRs
  └── A4: Welcome flow                                   ~1 PR

Track B (Reports content; parallel)
  ↓
  ├── B1: Cohort-5 ARR/MRR Stage-05 hand-off (ONR)       ~1 doc
  ├── B2: Cohort-5 Stage-06 build (Engineer + FED)       ~3 PRs
  ├── B3: Cohort-6 AP Aging Stage-05 hand-off (ONR)      ~1 doc
  └── B4: Cohort-6 Stage-06 build (Engineer + FED)       ~3 PRs

Track C (Demo polish; final pass)
  ↓
  ├── C1: Empty-state UX (FED)                           ~2 PRs
  ├── C2: Demo seed script (Engineer)                    ~1 PR
  ├── C3: Error-page polish (FED)                        ~1 PR
  └── C4: End-to-end demo dry-run (all hands)            ~1 doc
```

**Total demo-able-MVP delivery: ~16-18 PRs across 3 tracks + 2 ONR scaffolds + 1 dry-run doc.**

---

## 5. Cuts + deferrals

### Cosmetic (cut from MVP demo path)

- **Anchor desktop (Tauri) builds** — web-only acceptable for demo
- **Mobile field-capture (W#23)** — entirely defer
- **Cartridge marketplace UI** — defer (only the read-via-POST primitive exists; cartridge install flow not built)
- **Multi-tenant federation (signal-bridge)** — defer until post-MVP customer
- **Sub-cohort polish** (chart aesthetics, animations) — defer; basic Recharts default acceptable

### Precedent-critical (KEEP)

- **Tenant onboarding** — without it, the demo has no entry point
- **Audit-trail viewer** — proves the substrate (cohort-4 SHIPPED; KEEP visible in demo)
- **Reports** — ARR/MRR + AP Aging are the "value visualization" for the buyer; without them, demo is just CRUD
- **Financial cluster** — proves Sunfish is not "just another property tracker"

### Defer-decisions (route to Admiral)

- **Demo seed data quality** — is a synthetic 3-property demo enough, OR do we need a more realistic 50-unit demo seed? ONR recommends 50-unit synthetic for scale credibility.
- **Auth provider** — local-only auth for MVP demo OR integrate with Auth0/Clerk now? ONR recommends local-only for MVP demo speed; full auth deferred to first paying tenant.

---

## 6. Risk-weighted timeline

Assuming Engineer + FED parallel-shippable at observed velocity:

| Phase | Calendar | Risk | Notes |
|---|---|---|---|
| Cohort-5 + Cohort-6 Stage-05 (ONR) | Week 1 (2-3 days) | LOW | ONR Stage-05 hand-off velocity is well-characterized (V5 + V6 history) |
| Cohort-5 Stage-06 build (parallel with Cohort-6) | Week 2-3 (~7-10 days) | MEDIUM | Stage-06 builds at cohort-2 + cohort-3 + cohort-4 velocity = ~4 days each parallel |
| Onboarding-ladder Stage-02 + Stage-05 (Admiral + ONR) | Week 1-2 (parallel with B) (~3-5 days) | HIGH | First fully-cross-cohort flow; ADR 0091 + 0049 + 0094 all touched; needs adversarial brief |
| Onboarding-ladder Stage-06 build | Week 3-4 (~7 days) | HIGH | First user-facing flow with no precedent; design + UX iterations expected |
| Demo polish + seed script | Week 4 (~3-4 days) | LOW | Mechanical |
| End-to-end dry-run + iteration | Week 4-5 (~3-5 days) | MEDIUM | Real-world issues surface late |

**Calendar estimate: 4-5 weeks to demo-ready MVP at current observed velocity.**

**Compressible to: 3-3.5 weeks** if Cohort-5 + Cohort-6 + Onboarding-ladder run perfectly parallel (Engineer + FED both at 100% utilization on different tracks).

**Decompressed to: 6-7 weeks** if onboarding-ladder hits an architectural surprise (e.g., ADR 0091 needs Rev 3 for sign-up flow).

---

## 7. Highest-leverage next decisions

For Admiral routing:

1. **Approve onboarding-ladder scoping NOW** — ONR can author Stage-02 architecture scaffold in V8 batch; without it, demo MVP is blocked.
2. **Approve cohort-5 + cohort-6 Stage-05 hand-off authoring NOW** — ONR has the V5 #1a + #1b surveys ready; Stage-05 hand-offs are ~3-day deliverables each.
3. **Cut anchor-desktop from MVP demo scope explicitly** — frees Engineer/FED capacity.
4. **Decide auth provider question** — local-only vs Auth0/Clerk; ONR recommends local-only.
5. **Decide demo seed data scale** — 3-property vs 50-unit; ONR recommends 50-unit.

---

## 8. Comparison to MVP Phase work (cerebrum context)

Per cerebrum: "Default to Opus 4.7 for build sessions; Sunfish MVP-phase build sessions are common." This V7 #3 confirms: MVP phase is the dominant work-state through ~Week 5; build velocity matters more than process refinement during this window.

**Recommendation:** Admiral should reserve V7 + V8 + V9 batch directives mostly for **MVP critical-path enablement** (onboarding scoping, cohort-5/6 hand-offs, demo polish) rather than process work (more pattern catalog hygiene, more ADR refinement). Pattern + ADR work is high-quality compounding; MVP demo is time-critical.

---

## 9. What ONR does next

V7 #3 deliverable complete. Proceeds to V7 #7 (Security-officer agent UPF follow-up).

---

## 10. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T12-45Z` item #3
2. Cohort survey snapshots V5 #1a + #1b (shipyard ONR PRs)
3. V7 #2 cross-cohort dependency graph (shipyard#109)
4. V7 #6 pattern catalog snapshot (shipyard#108)
5. V5 #8 SPOT-CHECK SLA velocity baseline
6. ADR 0091 + 0092 + 0049 + 0094 (tenant-keying + audit substrate)
7. MASTER-PLAN.md (cohort-N→N+1 cadence)

— ONR, 2026-05-22T13:25Z
