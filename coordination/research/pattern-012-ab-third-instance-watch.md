# Pattern-012a/b — 3rd-instance forward-watch protocol

**Authored by:** ONR (V12 batch item #2)
**Requester:** Admiral (per `admiral-directive-2026-05-22T18-25Z` item V12 #2; follow-on to V11 #1 split)
**Authored at:** 2026-05-22T18-45Z

---

## Purpose

Per V11 #1 (shipyard#124) Option B split ruling, pattern-012 splits into:
- `pattern-012a-tenant-scoped-write-path` (1 instance)
- `pattern-012b-accountant-grade-write-path` (1 instance)

Each sub-pattern needs 2 more instances for formal ratification (per the catalog
3-instance rule). This V12 #2 protocol pre-identifies likely 3rd-instance
candidates so the catalog tracks anticipation + ONR + QM can cross-reference
on each new Bridge write-path PR.

---

## 1. Pattern-012a — 3rd-instance candidates

**Pattern signature recap (per V11 #1 §5.1):**
- Separated CSRF form (GET token → POST with X-XSRF-TOKEN header)
- NO idempotency (single-attempt write)
- Substrate-only audit emission (Bridge handler doesn't emit)
- AuthenticatedTenantPolicy (no role gating beyond authenticated)
- 4 typed error surfaces (E1-E4)
- E3 diagnostic-non-leak invariant (uniform-404 cross-tenant copy)

**1st instance:** cohort-2 PR 3 (sunfish#19 RentCollectionPage + signal-bridge#29 POST /payments) — MERGED

### 1.1 Candidate forward-watches

| Candidate PR / surface | Workstream | Likely scope | Pattern fit |
|---|---|---|---|
| **Maintenance write paths via Engineer V4 InMemoryMaintenanceService migration (V11 #3)** | W#74 (cohort-1 substrate) | Tenant guard write paths in Maintenance (`CreateVendorAsync`, `CreateMaintenanceRequestAsync`, `CreateWorkOrderAsync` invocation paths) | LOW — these are substrate-only operations; not Bridge POST endpoints |
| **Cohort-3 reports cluster (PAO #116; V10 #3)** | W#77 | `GET /api/v1/reports/{kind}` endpoints | NO — read-side, not write-side; pattern-009 only |
| **PR 5 Engineer ladder foundation-idempotency follow-on (shipyard#113)** | W#60 P4 | Touches Idempotency-Key store substrate | NO — substrate; not Bridge POST endpoint |
| **Future POST endpoints on Leases (signal-bridge cohort-1 follow-on)** | TBD | e.g., `POST /api/v1/leases/{id}/extend` | HIGH — likely pattern-012a if no idempotency / no role gating |
| **Future POST endpoints on Maintenance (signal-bridge)** | TBD | e.g., `POST /api/v1/work-orders/{id}/complete` | HIGH — same reasoning |
| **Future POST endpoints on Properties (signal-bridge)** | TBD | e.g., `POST /api/v1/properties/{id}/update-address` | HIGH — same reasoning |
| **Vendor write paths (cohort-2 vendor-onboarding extension)** | TBD | `POST /api/v1/vendors/{id}/onboard` etc. | MEDIUM — could be pattern-012a OR pattern-012b depending on idempotency need |

### 1.2 Most-likely 2nd-instance qualifier

**Onboarding-ladder sub-cohort 4 (Invitations) accept-flow** — particularly the
admin-side `POST /api/v1/invitations` endpoint:
- Authenticated admin (tenant-scoped)
- Single-attempt acceptable (no idempotency needed for simple "create invitation")
- E3 diagnostic-non-leak applies (cross-tenant probe behavior)
- 4-error-surface fit

If onboarding cohort-2 ships before any other Bridge POST endpoint, the invitation
admin-create endpoint is pattern-012a 2nd-instance.

### 1.3 Forward-watch tracking

ONR maintains a running log when:
- New Bridge POST endpoint PR opens
- Endpoint has authentication + CSRF + tenant-scoping
- No idempotency / no role-gating

When such a PR opens, ONR files an inline forward-watch comment in PR:

> ONR forward-watch (V12 #2): potential pattern-012a 2nd-instance candidate.
> Compare facets to sunfish#19 + signal-bridge#29 (1st instance) per V11 #1 §5.1.

---

## 2. Pattern-012b — 3rd-instance candidates

**Pattern signature recap (per V11 #1 §5.2):**
- Inlined CSRF in request body
- Mandatory Idempotency-Key header (24h TTL; 200-idempotent replay)
- Canonical N-field Bridge-layer audit emission
- Role-gated authorization policy
- 6 typed error surfaces (201 / 200-idempotent / 400 / 403 / 409 / 422)
- Closed-period 422 invariant (financial-specific)
- Exact decimal arithmetic invariant (financial-specific)

**1st instance:** signal-bridge#37 W#60 P4 PR 2 (POST /api/v1/financial/journal-entries) — OPEN

### 2.1 Candidate forward-watches

| Candidate PR / surface | Workstream | Likely scope | Pattern fit |
|---|---|---|---|
| **Journal entry void/reverse** (forward-watch in V8 #4 + cohort-2) | TBD | `POST /api/v1/financial/journal-entries/{id}/void` | **HIGH** — accountant-grade; needs idempotency; canonical audit emission |
| **AP invoice posting** (Bills cluster) | TBD | `POST /api/v1/financial/bills` posting flow | **HIGH** — accountant-grade; canonical audit; closed-period guard |
| **AR invoice posting** (Invoices cluster) | TBD | `POST /api/v1/financial/invoices` posting flow | **HIGH** — accountant-grade; canonical audit |
| **Payment application / unapply** (Payments cluster) | TBD | `POST /api/v1/financial/payment-applications/{id}/apply` | **HIGH** — accountant-grade; needs idempotency |
| **Period close** (Periods cluster) | TBD | `POST /api/v1/financial/periods/{id}/close` | **HIGH** — accountant-grade; idempotency on period operation |
| **Adjustment posting** (Adjustments cluster — future) | TBD | `POST /api/v1/financial/adjustments` | **HIGH** — accountant-grade; canonical audit |
| **Subscription transitions** (Subscriptions cluster) | TBD | `POST /api/v1/billing/subscriptions/{id}/upgrade` | **MEDIUM** — financial-adjacent but may not need closed-period |

### 2.2 Most-likely 2nd-instance qualifier

**AR invoice posting (`POST /api/v1/financial/invoices`)** — if it ships before
journal entry void/reverse. Reasoning:
- AR/Invoices clusters are higher up the financial substrate ladder (per cohort-2 PR 0a)
- Posting an invoice is THE canonical accountant operation (parallel to journal posting)
- Same shape: idempotency + canonical audit + AccountantPolicy + closed-period

**Journal entry void/reverse** — also strong candidate if it ships before AR
invoice posting (forward-watched in cohort-2 spec).

### 2.3 Forward-watch tracking

ONR maintains a running log when:
- New Bridge POST endpoint in `Sunfish.Bridge/Financial/` opens
- Endpoint has role-gating (Accountant or FinancialAdmin policy)
- Endpoint has Idempotency-Key requirement
- Endpoint has canonical audit emission

When such a PR opens, ONR files an inline forward-watch comment in PR:

> ONR forward-watch (V12 #2): potential pattern-012b 2nd-instance candidate.
> Compare facets to signal-bridge#37 (1st instance) per V11 #1 §5.2.

---

## 3. Cross-pattern facet conflict detection

A new Bridge POST endpoint may have facets that don't fit cleanly into 012a OR
012b. Watch for:

### 3.1 Hybrid facets (do NOT match either sub-pattern)

- Has Idempotency-Key BUT not role-gated → neither 012a (no idempotency) NOR 012b (role-gated)
- Has role-gating BUT no Idempotency-Key → fit-ambiguous
- Has canonical audit at Bridge BUT separated CSRF → fit-ambiguous
- Has separated CSRF + idempotency → fit-ambiguous

### 3.2 What to do if hybrid emerges

ONR files question to Admiral:
- "PR X has hybrid facets between 012a and 012b — does this constitute a new
  sub-pattern (012c) OR force a re-design to match one of the existing sub-patterns?"

Per V11 #1 §4.2 fallback option (Option C — single pattern with deferred variant),
Admiral may need to ratify either:
- Sub-pattern proliferation (012c, 012d, etc.)
- Pattern consolidation (merge facets; reconsider the split)

### 3.3 Hybrid forward-watch

ONR tracks any PR that fits 4+ of 012a's facets AND 4+ of 012b's facets. The
threshold for 012c proposal: 2+ hybrid PRs in same cohort.

---

## 4. QM cross-reference protocol

Per V12 #2 dispatch instruction "ONR + QM cross-reference on each new Bridge
write-path PR":

When a new Bridge POST endpoint PR opens:

1. **ONR** files forward-watch comment in PR within 30 min of Ready-flip
2. **QM** scans PR for facet conformance (CSRF form, idempotency presence,
   audit emission shape) — QM Phase 0 instrumentation per V3 addendum #9-#11
3. **ONR** updates this protocol's tracking section with the candidate
4. **Admiral** ratifies sub-pattern assignment if either ONR or QM flags
   ambiguity

QM daemon (per V10 #1 §1.2 + V12 #4 protocol) consumes this protocol's tracking
section as input.

---

## 5. Tracking log (ONR maintains; appends per emergence)

When candidates emerge, ONR appends:

```markdown
| Date | PR | Candidate sub-pattern | Facet check | ONR verdict |
|---|---|---|---|---|
| 2026-05-22 | sunfish#19 + signal-bridge#29 | 012a | matches all 6 facets | INSTANCE 1 (formal) |
| 2026-05-22 | signal-bridge#37 | 012b | matches all 7 facets | INSTANCE 1 (formal) |
| ... | ... | ... | ... | ... |
```

ONR updates this table when each new candidate emerges; the table feeds into V8 #6 post-cohort-10 retrospective metrics.

---

## 6. Ratification trigger criteria

For each sub-pattern, 3rd-instance ratification requires:

1. **Facet conformance** — all canonical facets present in the 3rd-instance PR
2. **Independent emergence** — 3rd instance was not consciously authored "to fit
   the pattern"; the team chose the facets independently based on requirements
3. **Council attestation** — sec-eng-council + .NET-architect-council both
   verdict GREEN/AMBER on the 3rd-instance PR

When all 3 criteria met:
- ONR drafts ratification scaffold for the formal catalog entry
- Admiral promotes from candidate → formal in `standing-approved-patterns.md`
- Pattern carries the 3-instance reference list

---

## 7. Decisions surfaced to Admiral

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **Forward-watch comment placement** — ONR files inline PR comment, OR separate
   beacon? ONR recommends inline PR comment (lighter; visible at PR review).
2. **QM Phase 0 protocol** — instrument facet-conformance check on every Bridge
   POST PR? Defer until QM V5 #3 lands?
3. **Hybrid pattern threshold (012c)** — 2 hybrid PRs in same cohort? Or 3
   total across cohorts? ONR recommends 2-in-cohort threshold.
4. **Pattern numbering for forward-watch candidates** — if 012c emerges, allocate
   immediately, or wait until 1st-instance lands? ONR recommends wait-until-first.

---

## 8. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T18-25Z` item V12 #2
2. V11 #1 pattern-012 canonical framing research (shipyard#124) — split into 012a + 012b
3. V8 #4 ADR 0093 Rev 2 scaffolding (shipyard#118) — V11 #1 split derived
4. V10 #1 Engineer substrate ladder (shipyard#121) — PR #5 foundation-idempotency forward-watch
5. cohort-2 PR 3 (sunfish#19 + signal-bridge#29) — 012a instance 1
6. signal-bridge#37 — 012b instance 1 (OPEN)
7. V12 #1 signal-bridge#37 description scaffold (shipyard#129) — sister PR
8. fleet-conventions §SPOT-CHECK + standing-approved-patterns.md (catalog 3-instance rule)

---

## 9. What ONR does next

V12 #2 protocol complete. Proceeds to V12 #3 (Engineer V3 #1 IAuditEventReader
supplementary spec; ~1-2h).

— ONR, 2026-05-22T18:45Z
