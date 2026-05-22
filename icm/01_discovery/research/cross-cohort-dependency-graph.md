# Cross-cohort dependency graph (2026-05-22)

**Authored by:** ONR (V7 batch item #2)
**Requester:** Admiral (per `admiral-directive-2026-05-22T12-45Z` item #2)
**Authored at:** 2026-05-22T13-05Z

**Path note:** Directive specifies `coordination/hand-offs/cross-cohort-dependency-graph.md` but that directory does not exist. ONR writes to canonical research path `shipyard/icm/01_discovery/research/`.

---

## Scope

Visual + structured diagram of cross-cohort dependencies across cohort-1 → cohort-6. Covers:
- Cohort dependency edges (which cohort needs which prior cohort shipped)
- Substrate ladders (ADR 0091, 0092, 0094)
- Pattern catalog dependencies (pattern-009 + tenant-keying-retrofit; pattern-012; pattern-013; pattern-014)
- Bridge endpoint families
- Critical path to MVP demo

---

## TL;DR

1. **Cohorts 1+2 BUILT** (W#74 + W#76; financial cluster + cohort-1 rebind shipped).
2. **Cohort-3 Stage-06 READY** (W#77; PAO Track C cohort-3 design pending; FED execution gated).
3. **Cohort-4 Stage-06 READY** (W#78; audit-trail viewer; ADR 0094 substrate landed via #100).
4. **Cohort-5 + Cohort-6 SCOPED** (per V5 #1a + V5 #1b; ARR/MRR + AP Aging recommendations).
5. **Critical-path observation:** cohort-3 + cohort-4 are PARALLELIZABLE (cohort-4 has no functional dependency on cohort-3 reports). Cohort-5 + cohort-6 also can parallelize (different surfaces). Bottleneck is PAO Track C design + sec-eng SPOT-CHECK throughput.

---

## 1. Cohort dependency graph (mermaid)

```mermaid
graph LR
    subgraph "Substrate Layer (Foundation)"
        ADR0091["ADR 0091 ITenantContext<br/>(Step 1 SHIPPED; 2.0+ in flight)"]
        ADR0092["ADR 0092 Substrate Tenant-Keyed<br/>Repository Contract (Accepted)"]
        ADR0094["ADR 0094 IAuditEventReader<br/>(Substrate landed at #100)"]
        ADR0093["ADR 0093 Stage-05 Adversarial<br/>Review (Accepted at #104)"]
    end

    subgraph "Cohort 1 (W#74; BUILT 2026-05-18)"
        C1[Properties + Leases + Maintenance<br/>+ close-out]
        C1_Policy[AuthenticatedTenantPolicy]
    end

    subgraph "Cohort 2 (W#76; BUILT 2026-05-21)"
        C2_Sub["PR 0a/b/c/d Substrate<br/>(tenant-keyed repos)"]
        C2_FED[PR 1/2/3 Frontend Cluster<br/>+ PR 4 Close-out]
        C2_P9TKR[pattern-009-tenant-keying-retrofit<br/>RATIFIED 2026-05-22]
    end

    subgraph "Cohort 3 (W#77; Stage-06 READY)"
        C3[Reports Cluster<br/>5 FED PRs + 1 Eng prereq]
        C3_P13[pattern-013-cartridge-read-via-post<br/>candidate]
    end

    subgraph "Cohort 4 (W#78; Stage-06 READY)"
        C4[Audit-Trail Viewer<br/>3 FED PRs + 1 Eng prereq]
        C4_R3["R3 Adversarial Brief<br/>(per ADR 0093)"]
    end

    subgraph "Cohort 5 (scoped V5 #1a)"
        C5[ARR/MRR Reporting<br/>~12-20h]
    end

    subgraph "Cohort 6 (scoped V5 #1b)"
        C6[AP Aging Page<br/>~5-7h]
    end

    ADR0091 -->|Step 2.0+| C2_Sub
    ADR0092 --> C2_Sub
    C2_Sub --> C2_P9TKR
    C2_Sub --> C2_FED
    ADR0094 --> C4
    ADR0093 --> C4_R3
    C1 --> C2_Sub
    C1_Policy --> C3
    C1_Policy --> C4
    C2_Sub --> C3
    C2_Sub --> C5
    C3 --> C6
    C3_P13 --> C6
```

---

## 2. Cohort completion + dependency table

| Cohort | Workstream | Status | Substrate dep | Frontend dep | Gates downstream |
|---|---|---|---|---|---|
| 1 | W#74 | BUILT 2026-05-18 | `AuthenticatedTenantPolicy` (cohort-1 PR 1) | None | Establishes policy precedent for cohort-2+ |
| 2 | W#76 | BUILT 2026-05-21 | ADR 0091 Step 1; ADR 0092 Step 1 substrate | Cohort-1 policy reuse | pattern-009-tenant-keying-retrofit ratified; sets cohort-3+ substrate baseline |
| 3 | W#77 | Stage-06 READY | W#72 blocks-reports cartridges (shipped); ADR 0091/0092 substrate | Cohort-2 frontend rebind pattern | PAO Track C design pending; AP Aging deferred → cohort-6 |
| 4 | W#78 | Stage-06 READY | ADR 0094 IAuditEventReader (shipped at #100); audit-emission retrofit (V2 #3 in flight) | Cohort-1/2 frontend pattern | First R3 Adversarial Brief pilot per ADR 0093 |
| 5 | (TBD W#79?) | Scoped | `blocks-subscriptions` accumulator gap | Cohort-1/2 frontend pattern | None active |
| 6 | (TBD W#80?) | Scoped | `ApAgingSummaryCartridge` (Engineer ~3-4h) | Cohort-3 cartridge consumption pattern | None active |

---

## 3. Substrate ladders (ADR-by-ADR)

### 3.1 ADR 0091 ITenantContext ladder (per V5 #5 + V6 #3)

```mermaid
graph TD
    Step1["Step 1: Foundation.Authorization package<br/>+ sum-interface facade (SHIPPED at #44)"]
    Step20["Step 2.0: SunfishBridgeDbContext rewrite<br/>(in flight per V1 #3 + V3 #3)"]
    Step21["Step 2.1+: Endpoint migrations narrow to<br/>Foundation.MultiTenancy.ITenantContext"]
    Step3["Step 3: Test fixture migration"]
    Step4["Step 4: Facade [Obsolete] +<br/>RequestContextMixingAnalyzer"]
    Step5["Step 5: Facade deletion (one-cohort grace)"]
    Step6["Step 6: foundation-authorization package<br/>boundary cleanup (V5 #5 Option A)"]
    Step1 --> Step20
    Step20 --> Step21
    Step21 --> Step3
    Step3 --> Step4
    Step4 --> Step5
    Step5 --> Step6
```

**Status:** Step 1 SHIPPED; Step 2.0+ in flight; ladder terminates at Step 6 per V6 #3.

### 3.2 ADR 0092 Substrate Tenant-Keyed Repository ladder

```mermaid
graph TD
    P0092_S1["Step 1: ITenantScopedRepository<TEntity, TKey> marker<br/>(SHIPPED at #47)"]
    P0092_S2["Step 2: HasQueryFilter + WhereTenant extension<br/>(extension SHIPPED at #102; per-cluster pending)"]
    P0092_S3["Step 3: Option A service-layer retention<br/>(documented; never-remove default)"]
    P0092_S4A["Step 4a: TenantFilterBypassAnalyzer"]
    P0092_S4B["Step 4b: WithoutQueryFiltersDocumentationAnalyzer"]
    P0092_S4C["Step 4c: TenantIdFirstParameterAnalyzer"]
    P0092_S1 --> P0092_S2
    P0092_S2 --> P0092_S3
    P0092_S3 --> P0092_S4A
    P0092_S3 --> P0092_S4B
    P0092_S3 --> P0092_S4C
```

**Status:** Step 1 SHIPPED; Step 2 WhereTenant SHIPPED (#102); per-cluster Step 2 + Steps 4a/b/c pending.

### 3.3 ADR 0094 IAuditEventReader ladder

```mermaid
graph TD
    P0094_S1["Step 1-5: IAuditEventReader substrate<br/>+ InMemoryAuditEventReader + DI + tests<br/>(SHIPPED at #100)"]
    P0094_Co4["Cohort-4 consumer:<br/>audit-trail viewer (W#78)"]
    P0094_S1 --> P0094_Co4
```

**Status:** SHIPPED at #100 (Admiral implementation per ONR V4 #2 scaffold + V6 #6 Option C HYBRID disposition).

---

## 4. Pattern catalog dependencies

```mermaid
graph LR
    P009[pattern-009<br/>Bridge endpoint + frontend pair<br/>FORMAL] --> P009TKR[pattern-009-tenant-keying-retrofit<br/>FORMAL 2026-05-22]
    P009 --> P012[pattern-012-financial-write-path<br/>candidate; 1st instance shipped]
    P009 --> P013[pattern-013-cartridge-read-via-post<br/>candidate; 1st instance cohort-3]
    P009 --> P014[pattern-014-bridge-cross-tenant-audit-emission<br/>UNVERIFIED 4 instances per directive]
    P012 -.->|3rd instance ratification| P012_W60P4PR2[W#60 P4 PR 2 JournalEntry POST]
    P013 -.->|3rd instance ratification| P013_C6[Cohort-6 AP Aging cartridge]
    P014 -.->|verification| Admin_Verify[Admiral confirms 4 shipped instances]
```

**Status (per V7 #6 audit):**
- pattern-009 + pattern-009-tenant-keying-retrofit: FORMAL on main
- pattern-012 + pattern-013: candidates; NOT on main (ONR V5 #2 #88 still OPEN)
- pattern-014: NOT on main; 4-instance claim unverified

---

## 5. Bridge endpoint families

| Family | Status | Cohort |
|---|---|---|
| `Sunfish.Bridge.Cockpit.*` (existing) | pre-restructure baseline | n/a |
| `Sunfish.Bridge.Properties.*` (cohort-1 PR 1) | SHIPPED | C1 |
| `Sunfish.Bridge.Leases.*` (cohort-1 PR 2) | SHIPPED | C1 |
| `Sunfish.Bridge.Maintenance.*` (cohort-1 PR 3) | SHIPPED | C1 |
| `Sunfish.Bridge.Financial.*` (cohort-2 PR 1/2/3) | SHIPPED | C2 |
| `Sunfish.Bridge.Reports.*` (cohort-3 Engineer prereq) | PENDING | C3 |
| `Sunfish.Bridge.Audit.*` (cohort-4 Engineer prereq; consumes IAuditEventReader) | PENDING | C4 |
| `Sunfish.Bridge.RecurringRevenue.*` (cohort-5 hypothetical) | not yet scoped at Stage-05 | C5 |
| `Sunfish.Bridge.Reports.ApAging.*` (cohort-6 via cartridge runner) | not yet | C6 |

---

## 6. Critical path to MVP demo (see also V7 #3)

Per V7 #3 (separate research), the critical path is:

```
Cohort-1 BUILT → Cohort-2 BUILT → {Cohort-3 OR Cohort-4 parallel} → Cohort-5 ARR/MRR → MVP demo
```

Cohort-3 + Cohort-4 are PARALLELIZABLE; cohort-5 is the first investor-grade story. Cohort-6 (AP Aging) is closure-not-critical for MVP.

---

## 7. Parallelization opportunities

| Cohort N + M | Can parallelize? | Why |
|---|---|---|
| C3 + C4 | YES | Different surfaces (reports vs audit); no shared FED files |
| C5 + C6 | YES (post-C3/C4) | Different surfaces (ARR vs AP Aging) |
| C4 + W#60 P4 PR 2 | YES | pattern-012 ratification at JournalEntry POST is separate from audit viewer |
| Engineer substrate ladder Phase 1-3 vs frontend cohorts | YES | Substrate work doesn't block frontend page work; FED execution gated on substrate at Stage-06 entry, not authoring |

---

## 8. Bottlenecks

1. **PAO Track C cohort-3 design** — gates cohort-3 FED execution (PR 2-5; PR 1 api layer can ship without)
2. **Sec-eng SPOT-CHECK throughput** — per V5 #8 SLA refinement; differentiated by PR type
3. **W#60 P4 PR 1 Stronghold completion** — gates W#60 P4 PR 2-5 + downstream OIDC ADR work
4. **Engineer post-cohort-2 substrate ladder** — 6 phases × ~21 PRs (V3 #3); long-running ladder
5. **pattern-014 verification** — V7 #6 audit found 0 merged instances; either Engineer hasn't tagged OR claim is aspirational

---

## 9. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **pattern-014 verification** — per V7 #6 audit; 4-instance claim returns 0 in shipyard merged PR search. Admiral confirms?
2. **`coordination/hand-offs/` directory creation** — V7 directive specifies this path; doesn't exist. Create new top-level coordination dir OR continue using `shipyard/icm/01_discovery/research/` and `shipyard/icm/_state/handoffs/`?

---

## 10. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T12-45Z` item #2
2. V5 #1a + #1b cohort-5/6 scope surveys (shipyard#95 + #96)
3. V3 #1 cohort-4 hand-off (shipyard#81 MERGED)
4. V1 #1 cohort-3 hand-off (shipyard#51 MERGED)
5. V3 #3 Engineer substrate sequencing (shipyard#79)
6. V5 #5 ADR 0091 Steps 5+6 (shipyard#91)
7. V6 #3 ADR 0091 ladder termination (shipyard#98)
8. V7 #6 pattern catalog snapshot (shipyard#108)
9. Cohort-1 hand-off + cohort-2 hand-off (shipyard#42 MERGED)
10. ADR 0091 R2 (Accepted) + ADR 0092 (Accepted) + ADR 0093 (Accepted #104) + ADR 0094 (substrate #100)

---

## 11. What ONR does next

V7 #2 deliverable complete. Files `onr-status-*-v7-item-2-cross-cohort-graph-complete.md`. Proceeds to V7 #5 (Stage-05 retro scaffolding).

— ONR, 2026-05-22T13:05Z
