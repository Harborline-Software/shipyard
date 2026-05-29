# Master Plan — Harborline Fleet (Sunfish ERP + Inverted Stack)

**Last updated:** 2026-05-21 (V4 #7 refresh by ONR per `admiral-directive-2026-05-21T14-05Z`; cohort-2 closed + pattern-009-tenant-keying-retrofit ratified + pattern-010→012 renumber + cohort-3 Stage-06 ready + cohort-4 Stage-05 ready + ADR 0091/0092 Accepted + Engineer post-cohort-2 substrate ladder defined)
**Maintained by:** Admiral (cross-fleet PM; formerly XO)
**Cadence:** updated when goal definition, milestone, or velocity baseline materially changes; not on every PR. The dynamic state lives in `active-workstreams.md` (shipyard repo); this file is the stable "where we're going."

> **Restructure note (2026-05-17):** The legacy `SunfishSoftware/` monorepo was superseded by the **Harborline Software fleet** — seven sibling repos under the `Harborline-Software` GitHub org with the platform substrate at `shipyard/`. Phase 2 of the restructure ratified by CIC the same day; see `/Users/christopherwood/Projects/Harborline-Software/RATIFICATION-2026-05-17.md`. All references in this file use the new repo names.

---

## The three goals

This effort has **three concurrent goals**, ranked by CIC priority:

| # | Goal | Primary repo(s) | Definition of done | Strategic role |
|---|---|---|---|---|
| **G-1** | **Business MVP** | `sunfish` (app) + `shipyard` (substrate) + `signal-bridge` (relay) | CIC's property management business runs on the Sunfish ERP app (the Anchor, hosted in `sunfish/`) + ERPNext composition model per W#60 UPF plan approved 2026-05-11. ERPNext (self-hosted, GPLv3) is the accounting + property engine; Sunfish provides the local-first sync + offline + React UI + tenant comms layer on top. 6 tenants (4 LLCs + holding co + mgmt co), spouse co-ownership active, full monthly cycle (rent → invoice → bank reconciliation → statements → vendor payments) running in production. Accountant peer node (Headscale) and CPA read-only access via `signal-bridge`. | **Primary.** Proves the local-first paradigm with a real commercial workload. |
| **G-2** | **Component library** | `shipyard` | Dual-namespace components (Rich vs MVP per ADR 0041) shipped with parity tests passing; style audit synthesis findings (248 findings, 10 themes — `project_style_audit_synthesis_2026_04`) remediated; compat-package expansion (Telerik / Syncfusion / DevExpress / Infragistics — `project_compat_expansion_workstream`) wave landed. | Secondary. Funds + de-risks future commercial customers; book Part IV implementation playbooks pull from this. |
| **G-3** | **Book — *The Inverted Stack*** | `the-inverted-stack` | All 20 chapters + preface + epilogue + 4 appendices through `icm/approved` → `icm/assembled`; audiobook pipeline through ACX submission target; published. Vol-1/Vol-2 Harborline rebrand sweep completed in parallel. | Secondary but parallel. Drives architectural rigor (the book commits Sunfish to specific package contracts via `inverted-stack-package-roadmap.md`); commercial positioning of Sunfish per `project_sunfish_reference_implementation`. |

> **Naming clarification:** "Sunfish" is now the ERP product name (the former Anchor app, lives in `sunfish/`). The fleet platform substrate previously called Sunfish is now `shipyard/`. The relay previously called Bridge is now `signal-bridge/`. The media-studio previously called Galley is now `flight-deck/`.

---

## G-1: Business MVP — current state + path to done

### Phase 1 (foundational primitives) — ~95% done

Per `project_business_mvp_phase_1_progress` memory, G1-G6 substrate is **all merged**. Remaining:

- **G6 host integration** — wire `RecoveryCompleted → SqlCipher rekey + persist to kernel-audit` in the Sunfish ERP app. Stage 06. Not yet started. Buildable on Win + Mac (per ADR 0044 amendment 2026-04-28 + ADR 0048 multi-backend MAUI; CIC's Mac is now updated to latest OS + Xcode).
- **G6 Razor UI** — `TrusteeSetup / InitiateRecovery / ApproveRecoveryRequest / PaperKey` pages. Stage 06. Not yet started. Same multi-platform build envelope.
- **G7 conformance baseline scan** — unblocked on substrate; gated on G6 host integration.

**Phase 1 completion estimate:** ~3-5 PRs remaining; ~1 week of focused Engineer (po-mac / po-win) work.

### Phase 2 (commercial scope) — ~25% done

**⚠ W#60 pivot (2026-05-11) materially changes workstreams A–G.** ERPNext now owns accounting, invoicing, bank reconciliation, and statements. Sunfish wraps it. Original workstream scopes below have been annotated with pivot impact.

| WS | Title | Status | Pivot impact | Estimated PRs |
|---|---|---|---|---|
| **A** | Sunfish team-context binding for 6 entities | Not started; ERPNext manages multi-entity natively; Sunfish needs team-switch UX wiring to ERPNext entity IDs (ADR 0032) | **Scope reduced** — ERPNext carries the data model; Sunfish adds the UI + team-context selection | 2-3 |
| **B** | Wave Accounting migration | **SUPERSEDED** — W#60 Phase 1 PASS (CIC entered data directly into ERPNext 2026-05-12); no Wave import tool needed | — | 0 |
| **C** | Bank ingest + reconciliation | **LIKELY SUPERSEDED at Sunfish layer** — ERPNext bank reconciliation + Plaid integration operate at the ERPNext layer (via Frappe connectors). Sunfish React UI displays reconciled data from ERPNext API. Remaining Sunfish scope: offline cache of reconciled statement data in the Tauri SQLite store (W#60 P3). | **CIC decision needed:** Does CIC want a dedicated Sunfish Plaid integration, or use ERPNext's native bank feed? Admiral recommends deferring until W#60 P3 ships and CIC sees the offline experience. | 0-2 |
| **D** | Payments (Stripe + ADR 0051) | **LIKELY SUPERSEDED at Sunfish layer** — ERPNext has native Stripe integration (Frappe Payment Gateway). Stripe webhooks go to ERPNext directly; Sunfish React UI reads payment status from ERPNext API. No separate signal-bridge Stripe integration needed. | **CIC decision needed:** ADR 0051 "outbound-payment event forwarding" may be a no-op given ERPNext native handling. Admiral recommends treating WS-D as done via ERPNext unless CIC identifies a gap. | 0-1 |
| **E** | Outbound messaging (SendGrid + ADR 0052) | Not started; **W#20 phases 4-9 ARE WS-E**; ONR re-authoring the hand-off (admiral directive 2026-05-17T23-15Z) | **Unchanged** — `blocks-crew-comms` is Sunfish's primary differentiator over ERPNext | 4-6 |
| **F** | Audit trail (kernel-audit + Tier 1 retrofit) | **scaffold merged 2026-04-28**; Tier 1 retrofit ready-to-build | Unchanged | 1-2 |
| **G** | Statement template + monthly job | **SUPERSEDED** — ERPNext generates invoices/statements natively; Sunfish role is React UI display + offline cache | — | 0 |
| **H** | Spouse co-ownership + recovery (ADR 0046 primitives) | Not started; gated on `Foundation.Recovery` scaffolding | **Unchanged** — cryptographic recovery is Sunfish-layer (`shipyard` substrate), not ERPNext | 4-6 |

**W#60 phase workstreams (new, 2026-05-11+):**

| WS | Title | Status | Estimated PRs |
|---|---|---|---|
| **W#60 P2** | React UI skin (6 screens + `@sunfish/ui-react`) | **BUILT 2026-05-13** (PRs #731+#732+#751+#752+#757+#758) | done |
| **W#60 P3** | Tauri v2 offline shell (Surface Pro) | **Ready-to-build** — ADR 0086 PR #737 MERGED 2026-05-17; CO status flip Proposed→Accepted is the only remaining gate | 3-4 |
| **W#60 P4** | Collaboration (accountant peer + CPA + tenant portal + bank CSV) | **Hand-off authored 2026-05-16** — gated on P3 PASS (CIC Surface Pro acceptance) | 5-6 |
| **W#60 P5** | `@sunfish/contracts` + rent roll + P&L + Schedule-E | **PR 1 (`@sunfish/contracts`) shipped 2026-05-16** (#847+#848 Bridge endpoint); PR 2+ (richer reporting) **now subsumed by W#72** (see below) | done at thin-slice; v2 via W#72 |

**W#72 — blocks-reports cluster (NEW, shipped 2026-05-17):**

`blocks-reports` is the read-side report-cartridge cluster. **All 7 PRs landed today (2026-05-17):** `IReportCartridge<,>` substrate + 5 Phase 1 MVP cartridges (Trial Balance, AR Aging, AP Aging, P&L by Property, Rent Roll v2 — supersedes the W#60 P5 thin slice). v1 Rent Roll deprecation path documented; no breaking change. This effectively closes the reporting half of W#60 P5 and replaces it with a richer surface.

**W#74 — Anchor React Rebind Cohort 1 (BUILT, closed 2026-05-18):**

Cross-stack workstream rebinding Sunfish React app pages from direct-ERPNext calls onto the Bridge cockpit pattern. **All 4 PRs MERGED 2026-05-17 → 2026-05-18.** Properties + Leases + Maintenance + close-out shipped. Workstream `built` ledger flip 2026-05-18.

**W#76 — Anchor React Rebind Cohort 2 — Financial Cluster (BUILT, closed 2026-05-21):**

Cohort-2 hand-off (`shipyard/icm/_state/handoffs/anchor-react-rebind-cohort-2-stage06-handoff.md`) authored by ONR 2026-05-18 (961 lines; shipyard#42 MERGED). 8-PR cluster (substrate PR 0a/b/c/d + frontend PR 1/2/3 + close-out PR 4) targeting AccountingPage + LeaseDetailPage payments + RentCollectionPage rebinds.

**Substrate PR 0 cluster (all MERGED 2026-05-20T23:40Z → 2026-05-21T00:16Z):**
- shipyard#52 PR 0a — `blocks-financial-ar` IInvoiceRepository tenant-keyed
- shipyard#57 PR 0b — `blocks-financial-ap` IBillRepository tenant-keyed
- shipyard#60 PR 0c — `blocks-financial-payments` IPaymentRepository + IPaymentApplicationRepository tenant-keyed
- shipyard#64 PR 0d — `blocks-financial-ledger` IJournalStore tenant-keyed
- shipyard#63 — pattern-009-tenant-keying-retrofit catalog corrigendum (dual SPOT-CHECK ratification)

**Pattern-009-tenant-keying-retrofit FORMALLY RATIFIED 2026-05-21** after 4 clean substrate shippings + dual sec-eng + .NET-architect SPOT-CHECK GREEN.

**Frontend PR 1/2/3 + close-out PR 4 — in-flight by FED + net-architect SPOT-CHECKs returned 2026-05-21T02-25Z/02-28Z/02-19Z**; merge cascade expected this week.

**W#77 — Anchor React Rebind Cohort 3 — Reports Cluster (Stage-06 READY, 2026-05-21):**

Cohort-3 hand-off (`shipyard/icm/_state/handoffs/anchor-react-rebind-cohort-3-stage06-handoff.md`; shipyard#51 MERGED 2026-05-21T07:42Z; 841 lines) targets 4 report pages — Trial Balance (new) + AR Aging (new) + ProfitAndLossByPropertyPage (rewrite from PLReport) + RentRoll v2 (rewrite). Consumes W#72 blocks-reports cartridges. 5 FED PRs + 1 Engineer prereq (Bridge cartridge-runner endpoint family). AP Aging deferred to cohort-4+ (cartridge not shipped). Pattern-011-cartridge-read-via-post candidate from PR 1 + Engineer prereq.

**Gates:**
- PAO Track C cohort-3 design direction pending (`shipyard/_shared/design/cohort-3/`); FED PR 2-5 await
- Engineer prereq PR 0 cartridge-runner endpoint family (~2-3h) when Admiral dispatches

**W#78 — Anchor React Cohort 4 — Audit-Trail Viewer (Stage-06 READY, 2026-05-21):**

Cohort-4 anchor selected: **C3 audit-trail viewer** per V2 #6 cohort-4 scope survey research. Hand-off (`shipyard/icm/_state/handoffs/cohort-4-c3-audit-trail-viewer-stage06-handoff.md`; shipyard#81; 659 lines) is the first canonical instance of the R3 Adversarial Brief protocol (per V3 #4 prototype). 4-PR cluster: 1 Engineer prereq (`GET /api/v1/audit-events` Bridge endpoint family with signed-cursor pagination + CSV export DoS protection) + 3 FED PRs (table view + detail page + close-out). ~6-9h dev. Consumes V2 #3 audit-emission Bridge retrofit (forensics surface).

**Phase 2 completion estimate (revised 2026-05-17):** ~12-25 PRs remaining (W#72 closed today; W#74 closing imminently; WS-C and WS-D likely 0-3 PRs combined if ERPNext native handles them). CIC decision on C+D needed after W#60 P3 ships.

### G-1 done conditions (concrete, revised 2026-05-17)

**Phase 1:**
- [x] `Foundation.Recovery` package split built (W#15 + W#32 both `built`)
- [ ] G6 host integration + Razor UI shipped (W#63 hand-off authored 2026-05-16 — immediately buildable; assigned po-mac / po-win)
- [ ] G7 conformance baseline scan committed under `shipyard/icm/01_discovery/output/`

**ERPNext composition layer (W#60):**
- [x] W#60 P1 PASS — ERPNext self-hosted on CIC machine; lease + rent payment + ledger confirmed (2026-05-12)
- [x] W#60 P2 BUILT — React UI (6 screens + `@sunfish/ui-react`) on main (2026-05-13)
- [x] W#60 P5 PR 1 BUILT — `@sunfish/contracts` published; Bridge thin-slice rent roll shipped (2026-05-16)
- [ ] W#60 P3 PASS — CIC works offline on Surface Pro 30 min; reconnects; changes appear in ERPNext (gates P4); ADR 0086 PR merged 2026-05-17, status flip pending
- [ ] W#60 P4 PASS — Accountant peer node syncing; CPA can view year-end data; tenant portal works via magic-link

**Reporting (NEW, supersedes W#60 P5 v2):**
- [x] W#72 blocks-reports cluster BUILT 2026-05-17 — Trial Balance, AR Aging, AP Aging, P&L by Property, Rent Roll v2 substrate + 5 cartridges + render pipeline

**Anchor React rebind cascade:**
- [x] W#74 Cohort-1 (Properties/Leases/Maintenance/close-out) — BUILT 2026-05-18
- [x] W#76 Cohort-2 substrate PR 0a-d — BUILT 2026-05-20/21 (pattern-009-tenant-keying-retrofit ratified)
- [ ] W#76 Cohort-2 frontend PR 1/2/3 + close-out PR 4 — In flight; net-architect SPOT-CHECKs GREEN 2026-05-21; merge cascade imminent
- [ ] W#77 Cohort-3 Reports (5 FED PRs + 1 Engineer prereq) — Stage-06 READY; gated on PAO Track C design + Engineer prereq dispatch
- [ ] W#78 Cohort-4 Audit-Trail Viewer (3 FED PRs + 1 Engineer prereq) — Stage-06 READY; first canonical R3 Adversarial Brief instance

**Engineer post-cohort-2 substrate ladder (V3 #3 plan):**

Per ONR V3 #3 sequencing research (shipyard#79): ~21 PRs / ~40-60h Engineer effort across 6 phases.

| Phase | Subject | Effort |
|---|---|---|
| 1 | Audit-emission Bridge retrofit (V2 #3) | 1-2h; unblocks forensics |
| 2 | ADR 0091 Step 2.0 DbContext rewrite | 3-4h; foundational |
| 3 | ADR 0092 Step 2 EFCore per-cluster | 8-12h × 4-8 PRs; parallelizable |
| 4 | ADR 0091 Step 3 test fixture migration | 12-24h batched |
| 5 | ADR 0091 Step 4 facade `[Obsolete]` + `RequestContextMixingAnalyzer` | 6-8h single PR |
| 6 | ADR 0091 Step 5 facade deletion | 1-2h; one-cohort grace |

~8-12 week ladder including the post-Step-4 one-cohort grace period.

**Phase 2 Sunfish-layer workstreams:**
- [x] ADR 0051 (Payments) Accepted 2026-04-28
- [x] ADR 0052 (Outbound messaging) Accepted 2026-04-28
- [x] **ADR 0091 (ITenantContext Divergence Resolution) Accepted 2026-05-19T02:40Z** — substrate for sum-interface facade decomposition (foundation-authorization)
- [x] **ADR 0092 (Substrate Tenant-Keyed Repository Contract) Accepted 2026-05-19T05:45Z** — cohort-2 PR 0a-d substrate; B6 relaxed 07:45Z
- [ ] **ADR 0093 (Stage-05 Adversarial Review Protocol Amendment) — drafting in progress** (authoring ownership pending Admiral routing per `onr-question-2026-05-21T14-08Z-v4-adr-authoring-scope`); V3 #4 prototype provides scaffold
- [ ] **ADR 0094 (IAuditEventReader; cohort-4 prerequisite) — drafting in progress** (same authoring ownership pending)
- [ ] WS-E built (W#20 phases 4-9 — providers-postmark adapter + inbound webhook + audit + docs; ONR addendum re-authored at shipyard#58 / V1 #4; Phase 10+ addendum at shipyard#75 / V2 #7; deeper Phase 10 research V4 #5 pending)
- [ ] WS-D: **CIC decision needed** — ERPNext native Stripe may make this a no-op; defer until W#60 P3 ships
- [ ] WS-A built (Sunfish team-context bound to ERPNext entities for 6-entity setup)
- [ ] WS-C: **CIC decision needed** — ERPNext native bank feed vs. dedicated Plaid integration; defer until W#60 P3
- [ ] WS-H built (spouse co-ownership + recovery)

**Business validation:**
- [ ] CIC processes first rent collection cycle end-to-end (React UI → ERPNext → bank statement)
- [ ] CIC sends first tenant communication through blocks-crew-comms
- [ ] Accountant performs bank reconciliation from their own Sunfish node
- [ ] CPA accesses year-end Schedule-E data via signal-bridge read-only session
- [ ] Spouse logs into her own Sunfish install with co-owner capabilities
- [ ] Recovery flow exercised end-to-end (real trustees, real grace period)
- [ ] Annual cycle dry-run: tax-prep export matches accountant's records

---

## G-2: Component library — current state + path to done

### Active workstreams (from existing memory)

- **Style audit remediation** — 248 findings, 10 systemic themes; 3-phase remediation in flight per `project_style_audit_synthesis_2026_04`. Synthesis at `shipyard/icm/07_review/output/style-audits/SYNTHESIS.md`.
- **Compat package expansion** — Telerik (existing) + Syncfusion + DevExpress + Infragistics. 4 intake decisions pending per `project_compat_expansion_workstream`. Queued behind current style-parity work.
- **Dual-namespace components** — Rich vs MVP per ADR 0041. SunfishGantt / Scheduler / Spreadsheet / PdfViewer. Both folders intentional per memory.
- **Adapter parity** — Blazor ↔ React per ADR 0014. Parity matrix maintained; CI gate planned for P6 (per ADR 0014 audit, this is partially honor-system today).

### G-2 done conditions (synthesized; needs CIC confirmation)

- [ ] Style audit remediation Phase 3 closed
- [ ] Compat-package expansion: 4 vendors complete (Telerik already shipped; Syncfusion / DevExpress / Infragistics to add)
- [ ] Adapter parity matrix at 100% across declared components; CI gate live
- [ ] Web Components track (ADR 0017) — M5 fan-out across 3 tracks complete (or formally deferred per the ADR 0017 audit recommendation)
- [ ] kitchen-sink demo covers every shipped component in every adapter

**G-2 completion estimate:** unclear without explicit done definition; placeholder ~30-50 PRs spread across multiple component waves.

---

## G-3: Book — current state + path to done

### Chapter inventory (file-system count, 2026-04-28)

| Part | Chapters in `book-structure.md` | Files exist | Likely status |
|---|---|---|---|
| Front matter | preface | (preface dir) | Drafting / late |
| Part I — Thesis & Pain | Ch01-04 | 4/4 .md files | All 4 issues at `icm/outline` per gh issue list |
| Part II — Council Reads the Paper | Ch05-09 | 5/5 .md files | Files present; ICM stages not surfaced via open issues — likely past outline |
| Part II — Council Reads the Paper | Ch05-10 | 5/6 .md files; **Ch10 (Synthesis) scheduled-pending per CIC 2026-04-28** | Synthesis closer; depends on Ch05-09 maturity |
| Part III — Reference Architecture | Ch11-16 | 5/6 .md files; **Ch16 (Persistence Beyond the Node) scheduled-pending per CIC 2026-04-28** | Ch15 most active (recent #46/#47 iterations); Ch16 consolidated from original Storage/Backup + Relay/Federation |
| Part IV — Implementation Playbooks | Ch17-20 | 4/4 .md files | Files present |
| Part V — Operational Concerns | Ch21+ | Ch21 only | Earliest part by file presence |
| Appendices | A-D | (appendices dir) | Unknown |
| Epilogue | (epilogue dir) | Present | Unknown |
| Audiobook | — | `build/` pipeline | Active recent investment (kokoro/higgs/ACX) |

**Total chapters in scope: 22** (Ch01-21 + the renumbered Part II Ch10).

### G-3 sub-track: Vol-1 / Vol-2 Harborline rebrand (NEW, 2026-05-17)

Parallel rebrand sweep authored by Admiral directive 2026-05-17T23-15Z (PAO direction + Yeoman execution). Scope: update Vol-1 and Vol-2 references to the new fleet naming (Sunfish ERP, shipyard, signal-bridge, flight-deck) where the legacy "Sunfish" platform usage no longer matches the post-restructure reality. **Track A held pending CIC ruling on anchor-name mapping** (PAO open question `pao-question-2026-05-18T01-38Z`); Track B pending. Coordinated parallel to the chapter pipeline; not on the critical path.

### G-3 open questions for CIC

- **Part V scope** — only Ch21 file present; is the rest of Part V planned?
- **Audiobook publishing target** — ACX submission counts as "published" for MVP, or wait for paperback?
- **Final-pass word-count trimming policy** (per CIC 2026-04-28): include all content first; final pre-publish pass strips word-count if needed.
- **Vol-1/Vol-2 rebrand anchor-name mapping** — PAO open question 2026-05-18T01-38Z; resolves the Track A held state.

### G-3 done conditions (synthesized)

- [ ] All chapters at `icm/approved` per book CLAUDE.md ICM pipeline
- [ ] All chapters at `icm/assembled` (added to `ASSEMBLY.md`)
- [ ] Vol-1/Vol-2 Harborline rebrand sweep complete (Tracks A + B)
- [ ] Foreword written + secured
- [ ] Final manuscript pandoc-assembled
- [ ] Audiobook ACX submission accepted

**G-3 completion estimate:** ~3-4 months at current velocity (see velocity baseline below). Rebrand sweep adds ~1 week parallel to chapter pipeline.

---

## Velocity baseline (updated 2026-05-17)

### Fleet PR throughput

**2026-04-28 baseline:** ~17 PRs/day in Sunfish (3-day average; bursty).

**2026-05-16 refresh:** W#23 + W#29 + W#44–W#62 cluster shipped across 2026-05-04–2026-05-16. Approximate total: 60–80 PRs merged in 18 days → **~4-5 substantive PRs/day sustained** in the legacy Sunfish monorepo.

**2026-05-17 burst (post-restructure):** ~30 PRs merged in a single day spread across the new fleet — W#72 blocks-reports + W#74 cohort-1 anchor-react rebinds + restructure rewires.

**2026-05-19 → 2026-05-21 cohort-2 + ONR research bursts:**
- 2026-05-19: ADR 0091 Step 1 + ADR 0092 cohort-2 substrate plan; W#23.3 iOS phases 1-3 across po-mac
- 2026-05-20: V1 ONR batch (5 research PRs ~2,221 lines; ADR 0091 Step 2.0 + W#60 P4 + WS-E phases 4-9 + OIDC scoping + cohort-3 hand-off); cohort-2 PR 0 cluster shipped 4 substrate PRs
- 2026-05-21: V2 ONR batch (7 PRs ~2,321 lines) + V3 ONR batch (6 PRs + 1 status ~1,844 lines) + pattern-010→012 renumber + V3 #4 Adversarial Brief prototype + V3 #1 cohort-4 Stage-05 hand-off

Velocity remains **bursty** — active build days hit 15-30 PRs; quiet days hit 0-2. **ONR observed velocity 2026-05-21:** V2 batch (7 items) shipped in 3h 25min; V3 batch (7 items) shipped in 17 min for the lighter doc deliverables. Sustainable substantive-feature pace: **4-8 PRs/day** sustained; ONR research bursts add 15-25 PRs/day at peak.

### Book throughput

26 book-update-loop iterations since 2026-04-15 (13 days) = **~2 chapter-stage-advancements per day**. With 8 ICM stages per chapter and 22 chapters × 8 stages = ~176 stage transitions to a finished book; subtract chapters already past outline (~14 chapters × ~3 stages average past = ~42 done) and add 2 fresh chapters (Ch10, Ch16) starting at outline = **~134 stage transitions + 16 (two new chapters from outline) = ~150 stage transitions remaining at ~2/day = ~75 working days = ~3-4 months**.

### Token-budget reality check

CIC on Pro Max ($200/mo). Recent overnight automation run consumed ~830K tokens total across 13 subagents + orchestration. That's a **roughly half-day burn at full intensity**. Repeating that pattern daily would consume the budget faster than necessary; **~2-3 such bursts per week** is sustainable while leaving headroom for normal work. Today's 30-PR burst, with multiple agents in flight (Engineer + FED + PAO + Yeoman + ONR + QM + po-mac + po-win), pushes the upper end of that envelope.

---

## Patterns + ADRs ratified since last refresh (2026-05-17 → 2026-05-21)

### Standing patterns

- **pattern-009-tenant-keying-retrofit** — RATIFIED FORMAL 2026-05-21 (4 clean substrate shippings: cohort-2 PR 0a-d at shipyard#52/57/60/64; dual sec-eng + .NET-architect SPOT-CHECK)
- **pattern-010 → pattern-012 renumber** (V3 #2 today, shipyard#77) — `pattern-010-financial-write-path` was originally proposed but pattern-010 + pattern-011 slots were already in use; renumbered to `pattern-012-financial-write-path`. 1st instance: cohort-2 PR 3 RentCollection POST. 3rd-instance ratification candidate: W#60 P4 PR 2 `POST /api/v1/financial/journal-entries` (per V3 #2 package-design + V2 #4 Candidate A scoring)
- **pattern-011 (cross-cluster event publisher wiring)** — candidate; needs 3 shippings (unchanged)

### ADRs Accepted

- **ADR 0091 — ITenantContext Divergence Resolution** — Accepted 2026-05-19T02:40Z; both councils GREEN on Revision 2
- **ADR 0092 — Substrate Tenant-Keyed Repository Contract** — Accepted 2026-05-19T05:45Z; B6 relaxed 07:45Z; cohort-2 PR 0a-d ratification trigger

### ADRs in drafting

- **ADR 0093 — Stage-05 Adversarial Review Protocol Amendment** — drafting authoring ownership pending Admiral routing (per `onr-question-2026-05-21T14-08Z-v4-adr-authoring-scope`)
- **ADR 0094 — IAuditEventReader (cohort-4 prerequisite)** — same routing pending

---

## Estimated MVP date — user-business-MVP (G-1)

**Revised 2026-05-21** (cohort-2 BUILT; pattern-009-tenant-keying-retrofit ratified; cohort-3 + cohort-4 Stage-06 hand-offs ready; Engineer post-cohort-2 substrate ladder mapped at ~40-60h):

| Track | Remaining work | Time estimate |
|---|---|---|
| Phase 1 G6 (Recovery host integration + UI) | W#63 hand-off authored 2026-05-16; immediately buildable; 3 PRs (po-mac / po-win) | 1-2 weeks |
| W#60 P3 (Tauri offline shell) | gated on CO status flip on ADR 0086 (PR merged 2026-05-17) | 1-2 weeks after CIC flips |
| W#60 P4 (Collaboration — accountant peer + CPA + tenant) | gated on P3 PASS | 2-3 weeks after P3 |
| ~~W#60 P5 (reporting)~~ | ~~superseded by W#72; thin-slice contracts shipped~~ | ✅ Done at thin-slice; v2 via W#72 |
| W#74 Anchor React Rebind Cohort 1 close-out | 2 PRs (PR 3 in flight; PR 4 close-out) | 1-2 days |
| Phase 2 Sunfish-layer (WS-A, C, D, E, H) | ~12-22 PRs; WS-E = W#20 phases 4-9 (ONR re-authoring); WS-H gated on W#63 + W#A | 3-5 weeks |
| ~~ADR 0051 + 0052 drafting~~ | ~~research~~ | ✅ Both Accepted 2026-04-28 |
| Business validation cycle | CIC-time-bound; first real rent-collection cycle | 2-4 weeks real-world |

**Estimated G-1 MVP-ready: 8-13 weeks from now** (mid-July to mid-August 2026), assuming:
- CIC flips ADR 0086 to Accepted + confirms Surface Pro P3 test within 1-2 weeks
- WS-E hand-off re-authored by ONR + picked up by Engineer
- Engineer + po-mac + po-win + FED run ~3-5 days/week
- No major blocking surprises in Tauri/Headscale peer connectivity

Today's 30-PR burst was predominantly CI/restructure rewiring + the W#72 reports cluster + W#74 anchor-react rebinds — **infrastructure and reporting wins, not MVP-blocker unlocks**, so the timeline shifts only marginally inward (the prior estimate was 8-14 weeks; this is 8-13 weeks). The bigger Phase 2 critical path (W#60 P3 → P4) remains gated on CIC actions.

**The ERPNext pivot accelerated the accounting/invoicing/reconciliation track** (B + G superseded; C + D scopes halved), and W#72 closes the reporting track. Net effect from the restructure + today's burst: ~1 week pulled in, with a much lower implementation risk profile on reporting.

---

## Update protocol

This file is updated when:
- A goal's done conditions change (CIC decision)
- A major workstream is added or removed
- Velocity baseline materially shifts (e.g., new automation tier, CIC availability changes, fleet restructure)
- Estimated MVP date changes by more than 1 week

**The Harborline restructure ratified 2026-05-17.** All future updates use Harborline fleet repo names (`shipyard`, `sunfish`, `signal-bridge`, `flight-deck`, `tender`, `coordination`, `the-inverted-stack`) and the canonical org chart (Admiral / Engineer / FED / PAO / Yeoman / ONR / QM / po-mac / po-win) — see `coordination/ORG-CHART.md`. Retired ranks (XO, COB, sunfish-PM, dev) should not appear in new content here.

**Day-to-day status lives in `shipyard/icm/_state/active-workstreams.md`. This file does not duplicate that.**

CIC receives an **executive summary** on demand, synthesized from this file + active-workstreams + recent gh data — see the "Status format" section in the parent `CLAUDE.md` § Multi-Session Coordination.

---

## Reference docs

- `shipyard/icm/_state/active-workstreams.md` — dynamic workstream ledger
- `shipyard/icm/_state/handoffs/` — Admiral → Engineer hand-off specs
- `shipyard/icm/00_intake/output/phase-2-commercial-mvp-intake-2026-04-27.md` — Phase 2 scope
- `shipyard/icm/05_implementation-plan/output/business-mvp-phase-1-plan-2026-04-26.md` — Phase 1 plan
- `shipyard/icm/07_review/output/adr-audits/CONSOLIDATED-HUMAN-REVIEW.md` — pending ADR amendment decisions
- `shipyard/docs/specifications/inverted-stack-package-roadmap.md` — Sunfish-side roadmap (mirror of book-side authoritative)
- `the-inverted-stack/inverted-stack-book-plan.md` — book writing plan
- `the-inverted-stack/book-structure.md` — chapter targets
- `/Users/christopherwood/Projects/Harborline-Software/RATIFICATION-2026-05-17.md` — Phase 2 restructure ratification record
- `/Users/christopherwood/Projects/Harborline-Software/coordination/ORG-CHART.md` — canonical fleet org chart
- `/Users/christopherwood/Projects/Harborline-Software/coordination/README.md` — coordination protocol
