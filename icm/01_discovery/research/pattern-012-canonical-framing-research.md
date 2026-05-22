# Pattern-012-financial-write-path — canonical framing research

**Authored by:** ONR (V11 batch item #1 HIGH PRIORITY)
**Requester:** Admiral (per `admiral-directive-2026-05-22T17-15Z` item V11 #1)
**Authored at:** 2026-05-22T17-30Z
**Trigger:** .NET-architect council RED-flag on shipyard#113/signal-bridge#37 ratification framing (council-verdict-2026-05-22T1234Z)

---

## 1. Problem statement

`.NET-architect council verdict 2026-05-22T1234Z` flagged THREE structural issues with the
pattern-012-financial-write-path ratification framing:

1. **Catalog has no `pattern-012` entry** — `standing-approved-patterns.md` formal patterns
   stop at `pattern-009-tenant-keying-retrofit`; pattern-012 is referenced in commit messages
   + PR descriptions + Admiral directives but never registered as a catalog entry.
2. **2nd instance is explicitly unaccounted-for in the spec ('TBD')** — sunfish#19 says
   "first instance"; signal-bridge#37 claims "3rd-instance ratification trigger"; the
   2nd instance is missing or never declared.
3. **The canonical CSRF form changes mid-ratification** — sunfish#19's CSRF was a
   separated-form (GET token + POST with header); signal-bridge#37's CSRF is inlined
   in the request body. These are NOT the same pattern.

V11 #1 deliverable: inventory the candidate instances + propose canonical framing.

---

## 2. Candidate instance inventory

### 2.1 Instance #1 — sunfish#19 + signal-bridge#29 (cohort-2 RB-9 PR PAIR)

**Status:** MERGED (sunfish#19 2026-05-21T13:30Z; signal-bridge#29 2026-05-21T08:45Z)
**Workstream:** W#76 cohort-2 PR 3 (FE) + PR 1 (BE)
**Pattern claim:** sunfish#19 tagged `@candidate-pattern: pattern-010-financial-write-path` (note original numbering — renumbered to 012 per V3 #2)

| Facet | Value |
|---|---|
| **CSRF form** | **Separated form** — `GET /antiforgery-token` returns `{ token, headerName: "X-XSRF-TOKEN" }`; `POST /payments` carries `X-XSRF-TOKEN` request header |
| **Token scope** | Function-local `let` binding (never persisted; consumed exactly once) |
| **Idempotency** | **NONE** — no `Idempotency-Key` header; no replay protection |
| **Audit emission (Bridge handler)** | NONE in Bridge layer; substrate-side emission only (`InMemoryPaymentRepository` 5-field canonical per V10 #2) |
| **Audit payload shape** | N/A at Bridge; 5-field canonical at substrate |
| **Authorization policy** | `AuthenticatedTenantPolicy` (cohort-1 default) |
| **Tenant resolution** | Server-derived from `ITenantContext`; DTO carries no `tenant_id` |
| **Closed-period handling** | N/A (Payment, not Journal Entry) |
| **Error surfaces** | E1 (network/unknown) / E2 (token-rejection) / E3 (lease-not-found uniform-404) / E4 (server error) — 4 typed |
| **Diagnostic-non-leak** | E3 carries IDENTICAL copy whether lease missing OR cross-tenant probe (verified in test) |
| **DI lifetime** | Substrate singletons; Bridge handler scoped per-request |
| **Cross-tenant guarantee** | Uniform-404 per ADR 0092 §A3 |

**Pattern shape signature:** "Read-write tenant-scoped Bridge POST with separated CSRF + substrate audit emission + typed E-codes + diagnostic-non-leak"

### 2.2 Instance #2 — MISSING / UNACCOUNTED

`.NET-architect verdict:` "the 2nd instance is explicitly unaccounted-for in the spec ('TBD')".

**Observed state:** signal-bridge#37 claims "3rd-instance ratification trigger" but jumps from #1 to #3 without a #2 declaration. Possible candidates that COULD have been #2 (none formally claimed):

- **cohort-2 PR 0a/b/c/d substrate (shipyard repo)** — tenant-keyed repository writes. NOT a Bridge write-path pattern; these are substrate-only.
- **signal-bridge#31 (cross-tenant audit emission retrofit)** — retrofitted existing endpoints; NOT a new write-path.
- **signal-bridge#33 (cross-tenant audit tranche 2)** — same — retrofit, not new write-path.
- **Any other cohort-2/cohort-3 financial write-path?** — none observed in PR history with explicit `@candidate-pattern: pattern-012` tag.

**Verdict:** **There is no genuine 2nd instance.** signal-bridge#37 is structurally instance #2, not #3. The "3rd instance" claim is incorrect.

### 2.3 Instance #3 (claimed) / actually Instance #2 — signal-bridge#36+#37 (W#60 P4 PR 2)

**Status:** signal-bridge#36 CLOSED; signal-bridge#37 OPEN (re-opened after #36 closed)
**Workstream:** W#60 P4 PR 2 (Journal Entries POST)
**Pattern claim:** `@candidate-pattern: pattern-012-financial-write-path (3rd-instance ratification trigger)`
**Sibling:** shipyard#113 (foundation-idempotency substrate) — OPEN

| Facet | Value |
|---|---|
| **CSRF form** | **Inlined in body** — `POST /api/v1/financial/journal-entries` request body carries antiforgery token as field; no separated GET endpoint |
| **Token scope** | Per-request; CSRF fail-fast BEFORE other validation (sec-eng SPOT-CHECK criterion 4) |
| **Idempotency** | **MANDATORY `Idempotency-Key` header** — substrate `foundation-idempotency` keyed on (key, tenant); 200-idempotent replay surface |
| **Audit emission (Bridge handler)** | **MANDATORY 7-field canonical at Bridge layer** — emit on happy path |
| **Audit payload shape** | **7-field canonical**: `entry_id`, `chart_id`, `posting_date`, `line_count`, `total_debits`, `total_credits`, plus extras (`memo`-truncated / `idempotency_key` / `correlation_id`) |
| **Authorization policy** | `AccountantPolicy` (NEW; with `@deviation-from-spec` forward-watch on role-claim enforcement) |
| **Tenant resolution** | Server-derived from `ITenantContext`; DTO carries no `tenant_id`; cross-tenant chart probe returns `chart_not_found` (400) |
| **Closed-period handling** | 422 `closed_period` (PeriodLocked / PeriodSoftClosed without FinancialAdmin role) |
| **Error surfaces** | Typed UNION of 6 surfaces: **201 / 200-idempotent / 400 / 403 / 409 / 422** |
| **DI lifetime** | Substrate singletons; Bridge handler scoped per-request |
| **Cross-tenant guarantee** | Chart probe + Account-code rejection at substrate boundary (per IAccountResolver.EnumerateForChartAsync) |
| **Decimal arithmetic** | Exact decimal DR/CR comparison; no float; no epsilon (sec-eng SPOT-CHECK criterion 6) |

**Pattern shape signature:** "Accountant-grade write-path with inlined CSRF + Idempotency-Key + 7-field canonical audit + closed-period 422 + role-gated"

### 2.4 Variation matrix

| Facet | Instance 1 (sunfish#19+signal-bridge#29) | Instance 2 (signal-bridge#37) | Same? |
|---|---|---|---|
| CSRF form | Separated | Inlined | **NO** |
| Idempotency | None | Required | **NO** |
| Audit emit at Bridge | None | Required (7-field) | **NO** |
| Authorization policy | AuthenticatedTenantPolicy | AccountantPolicy | **NO** |
| Closed-period surface | N/A | 422 | **NO** |
| Error surface count | 4 (E1-E4) | 6 (201/200-idem/400/403/409/422) | **NO** |
| Tenant resolution | Server-derived ✓ | Server-derived ✓ | YES |
| Cross-tenant uniform-404 | ✓ | ✓ (variant: 400 chart_not_found) | PARTIAL |
| Diagnostic-non-leak | ✓ | (not directly verified; assumed) | LIKELY YES |
| Decimal exactness | N/A | ✓ | (instance 1 N/A) |

**6 of 10 facets differ.** These are not the same pattern.

---

## 3. .NET-architect verdict implications

The verdict says "canonical CSRF form changes mid-ratification" — translating
that into our findings:

The promotion criterion for pattern-012 is "3rd instance lands → formalize." But:
- Instance 1 demonstrated separated CSRF
- Instance 2 (the "3rd") changes the CSRF form fundamentally
- The pattern has not stabilized

Per .NET-architect: **ratifying pattern-012 in its current framing would lock in
contradictory invariants**. Engineers reading the formal pattern would not know
which CSRF form to follow. Sec-eng-council reviewing future PRs against the
formal pattern would not have a stable yardstick.

---

## 4. Proposed canonical framing — 3 options

### 4.1 Option A — Single pattern with optional facets

`pattern-012-financial-write-path` = "Financial write-path" with optional facets:
- CSRF: separated form OR inlined body (PR's choice)
- Idempotency: none OR Idempotency-Key keyed
- Audit emit: none OR canonical N-field

**Pros:** Single catalog entry; flexibility for future variants.
**Cons:** Dilutes invariants; sec-eng-council cannot enforce against an "optional" facet without verdict density blowup; formal-pattern shorthand becomes "any financial POST" which is too broad.

**ONR verdict on Option A: REJECT.** Loses the pattern's load-bearing structure.

### 4.2 Option B — Split into 2 sub-patterns (RECOMMENDED)

- **`pattern-012a-tenant-scoped-write-path`** (1 instance — sunfish#19 + signal-bridge#29):
  - Separated CSRF form (`GET /antiforgery-token` + `POST` with `X-XSRF-TOKEN` header)
  - NO idempotency (single-attempt write)
  - Substrate-only audit emission (5-field canonical per V10 #2)
  - `AuthenticatedTenantPolicy`
  - 4 typed error surfaces (E1-E4)
  - Diagnostic-non-leak invariant on E3 (cross-tenant)

- **`pattern-012b-accountant-grade-write-path`** (1 instance — signal-bridge#37):
  - Inlined CSRF in request body (no separated GET)
  - **Mandatory** `Idempotency-Key` header (200-idempotent replay)
  - Bridge-layer audit emission, **canonical 7-field shape**
  - Role-gated policy (`AccountantPolicy`)
  - 6 typed error surfaces (201/200-idempotent/400/403/409/422)
  - Closed-period 422 invariant
  - Exact decimal DR/CR comparison invariant

Each sub-pattern is ONE instance. Both await 2nd-instance qualification before
3rd-instance ratification.

**Pros:**
- Each sub-pattern has crisp invariants — sec-eng-council reviews against a stable yardstick
- Future variants get their own sub-pattern letter (`-012c`, `-012d`)
- Acknowledges the genuine structural difference instead of papering over it
- Engineer + .NET-architect catalog framing is honest

**Cons:**
- Adds catalog complexity (one more entry)
- "Which sub-pattern is this?" decision required at PR-author time (mitigated by clear facet criteria)

**ONR verdict on Option B: STRONGLY RECOMMEND.**

### 4.3 Option C — Tighten pattern-012 to one canonical shape; defer the variant

`pattern-012-financial-write-path` = the **accountant-grade shape** (signal-bridge#37 type).
- Inlined CSRF + Idempotency-Key + canonical audit + role-gated + 6 surfaces

The cohort-2 sunfish#19 + signal-bridge#29 PAIR is NOT pattern-012; it's
**pattern-009 + a "simple tenant-scoped POST" candidate** which can become its
own pattern later if more instances emerge (or not — could remain pattern-009
without sub-pattern designation).

**Pros:** Cleanest catalog (one pattern-012); accountant-grade shape is the
"hard" pattern that justifies the catalog entry; simple tenant-scoped POSTs are
covered by pattern-009 alone.

**Cons:** Loses the explicit accounting of the simple variant; cohort-2's
sunfish#19 retroactively loses its `@candidate-pattern` tag (or it stays as
pattern-009 only).

**ONR verdict on Option C: ACCEPTABLE as fallback if Option B rejected.**

---

## 5. ONR recommendation — Option B with explicit migration

**Adopt Option B.** Migration sequence:

### 5.1 Catalog amendment (Admiral authors final per ADR-authoring-precedent)

Append to `shipyard/_shared/engineering/standing-approved-patterns.md` "Patterns proposed but not yet ratified" section:

```markdown
### `pattern-012a-tenant-scoped-write-path` — Tenant-scoped Bridge POST with separated CSRF

Tier: candidate (1 of 3 instances toward ratification)

**Canonical signature:**
- Bridge POST endpoint guarded by `AuthenticatedTenantPolicy`
- CSRF via separated form: companion `GET /antiforgery-token` returns
  `{ token, headerName: "X-XSRF-TOKEN" }`; POST carries `X-XSRF-TOKEN` request header
- Token consumed exactly once; never persisted; function-local scope
- Single-attempt write (no idempotency); replay attempt is undefined behavior
- Server-derived `tenant_id` from `ITenantContext`; DTO carries no tenant field
- Substrate-layer audit emission (Bridge handler does NOT emit; substrate emits)
- 4 typed error surfaces: E1 network / E2 token-rejection / E3 entity-not-found
  uniform-404 / E4 server-error
- E3 carries IDENTICAL copy whether entity missing OR cross-tenant probe
  (diagnostic-non-leak per ADR 0092 §A3)

**Instances:**
- Instance 1: cohort-2 PR 3 (sunfish#19 + signal-bridge#29 PAIR) — Payments
- Instance 2: TBD — qualifying PR per facet conformance
- Instance 3: TBD — promotion trigger
```

```markdown
### `pattern-012b-accountant-grade-write-path` — Accountant-grade Bridge POST with idempotency + canonical audit

Tier: candidate (1 of 3 instances toward ratification)

**Canonical signature:**
- Bridge POST endpoint guarded by domain-specific role policy
  (e.g., `AccountantPolicy`); 403 surface for non-role caller
- CSRF inlined in request body; antiforgery validation fail-fast BEFORE other validation
- **Mandatory** `Idempotency-Key` header; 24h TTL replay window;
  200-idempotent replay surface (per foundation-idempotency / pattern-012b)
- Server-derived `tenant_id` from `ITenantContext`; DTO carries no tenant field
- Substrate idempotency scope = (key, tenant); cross-tenant replay isolation
- Bridge-layer audit emission, **canonical N-field shape** (N varies by aggregate;
  Journal Entry uses 7-field per signal-bridge#37; future Bills/Receivables may
  use 8-field)
- Aggregate-specific invariants enforced server-side:
  - Closed-period 422 (`closed_period` reason)
  - Decimal arithmetic: exact comparison; no float; no epsilon
  - Cross-tenant chart/account probe: substrate boundary rejection
- 6 typed error surfaces: **201 / 200-idempotent / 400 / 403 / 409 / 422**

**Instances:**
- Instance 1: W#60 P4 PR 2 (signal-bridge#37 + shipyard#113 sibling) — Journal Entries
- Instance 2: TBD
- Instance 3: TBD — promotion trigger
```

### 5.2 PR amendments needed

- **sunfish#19 PR description retro-edit** — change `@candidate-pattern:
  pattern-010-financial-write-path` → `@candidate-pattern: pattern-012a-tenant-scoped-write-path`.
  (Already merged; cannot rewrite. Authorial intent preserved via commit history.)
- **signal-bridge#37 PR description amend** — change `@candidate-pattern:
  pattern-012-financial-write-path (3rd-instance ratification trigger)` →
  `@candidate-pattern: pattern-012b-accountant-grade-write-path (1st instance)`.
  Update the `@deviation-from-spec` line accordingly.
- **Future PRs** — tag against `-012a` or `-012b` per facet conformance.

### 5.3 Forward-watch for 2nd + 3rd instances

For pattern-012a 2nd-instance qualification, watch for:
- **Single-attempt POST** with separated CSRF + diagnostic-non-leak
- Likely candidates: future Bills/Vendors/Invoices/Quotes write-paths
  if they adopt separated CSRF without idempotency

For pattern-012b 2nd-instance qualification, watch for:
- **Idempotent accountant-grade POST** with inlined CSRF + canonical audit
- Likely candidates: Bills/Receivables/Adjustments POST endpoints
  if they follow the W#60 P4 PR 2 pattern

**3rd-instance ratification (each separately):** when the 3rd qualifying instance
lands, ONR drafts ratification scaffold; Admiral promotes the sub-pattern to
formal in catalog.

### 5.4 Pattern numbering coordination

Per V10 #3 + V9 #3 pattern numbering reconciliation:
- pattern-012a/012b use lettered sub-numbering — does NOT conflict with PAO #116
  cohort-3 patterns (which will renumber to 015/016/017)
- Future emergent candidates (per V9 #1 + V10 #1 forward-watches) still get
  pattern-018+ allocations

ONR recommends explicit pinning by Admiral when sub-patterns reach 2nd-instance.

---

## 6. Audit-payload shape: 5-field vs 7-field vs 8-field reconciliation

Per V10 #2 + V11 #1 inventory, observed audit-payload shapes:

| Shape | Used at | Fields |
|---|---|---|
| **5-field canonical** (ADR 0092 §A6) | Substrate-layer TenantBoundaryViolation emission | entity_type, entity_id, requested_tenant, actual_tenant, correlation_id |
| **7-field canonical** (pattern-012b accountant-grade) | Bridge-layer JournalEntryPosted emission | entry_id, chart_id, posting_date, line_count, total_debits, total_credits, + extras (`memo`-truncated, `idempotency_key`, `correlation_id`) |
| **8-field** (referenced in directive; unclear instance) | UNKNOWN — directive mentions but no instance located | — |
| **3-field non-canonical** | InMemoryMaintenanceService:210-222 (V10 #2 finding) | entity_type, entity_id, observed_tenant (non-canonical name) |

**Reconciliation:**
- 5-field is canonical for **violation events** (TenantBoundaryViolation specifically)
- 7-field is canonical for **happy-path posting events** (JournalEntryPosted; other accountant-grade writes likely 7+ depending on aggregate)
- 3-field is **non-canonical** per V10 #2; migration recommended

Different event types have different canonical payloads — that's correct, not
contradictory. The pattern-012b shape spec MUST name the canonical N-field shape
per aggregate type (Journal: 7; Bill: TBD; Invoice: TBD).

**Forward-watch:** as more accountant-grade write-paths emerge (Bills, Invoices,
Adjustments), the per-aggregate canonical N-field shapes should be documented
in the pattern-012b entry's "Instances" table.

The "8-field" reference in the V11 directive may be a misread; ONR couldn't
locate an 8-field emitter. If Admiral/Engineer can point to one, the
reconciliation table extends accordingly.

---

## 7. Pattern-013-cartridge-read-via-POST adjacency

Mentioned in directive but separate from pattern-012 framing. V10 #3 noted that
PAO #116 cohort-3 introduced 3 candidate patterns that conflict numbering-wise:
- PAO cohort-3 `pattern-013-csv-export-affordance` collides with existing
  `pattern-013-cartridge-read-via-POST` (V5 #2 #88 candidate)

Per V9 #3 Admiral ruling: PAO renumbers to pattern-015/016/017.

**This does NOT affect pattern-012a/012b framing** — letter-suffixed sub-patterns
are orthogonal to numeric collision-resolution.

---

## 8. Decisions surfaced to Admiral

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **Adopt Option B (split) — confirm?** ONR strongly recommends.
2. **Catalog amendment authoring** — Admiral authors final per ADR-authoring-precedent?
3. **signal-bridge#37 PR description amend** — Engineer authors amendment PR?
   ONR can scaffold; Engineer applies.
4. **Per-aggregate canonical N-field shapes** — document inline in pattern-012b
   "Instances" table as each new aggregate ships? Or single canonical 7-field
   with deviation tags?
5. **2nd-instance qualification for pattern-012a + pattern-012b** — which future
   PRs are forward-watched as candidates? ONR proposal (per §5.3):
   - 012a: future Bills/Vendors/Invoices write-paths with separated CSRF
   - 012b: Bills/Receivables/Adjustments accountant-grade writes
6. **8-field audit shape** — does the directive reference an extant emitter ONR
   missed, or is "8-field" a forward-looking shape? Admiral clarification.
7. **Pattern numbering pin** — pin pattern-018+ allocations for emerging candidates
   (V10 #1 + V9 #1 forward-watches)? Or defer until 2nd-instance?
8. **Sub-pattern naming convention** — lettered (`-012a`, `-012b`) ratified as
   fleet convention? Or use full slug names per other patterns?

---

## 9. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T17-15Z` item V11 #1
2. `coordination/inbox/council-verdict-2026-05-22T1234Z-net-architect-...` (referenced; ONR routes if not in inbox)
3. sunfish#19 PR description (MERGED 2026-05-21T13:30Z) — instance 1 frontend half
4. signal-bridge#29 PR description (MERGED 2026-05-21T08:45Z) — instance 1 Bridge half
5. signal-bridge#37 PR description (OPEN; `feat(bridge): POST /api/v1/financial/journal-entries — pattern-012 3rd-instance W#60 P4 PR 2`) — instance 2 (claimed 3rd; actually 2nd)
6. signal-bridge#36 (CLOSED; earlier attempt at instance 2)
7. shipyard#113 (OPEN; foundation-idempotency substrate) — pattern-012b substrate
8. `shipyard/_shared/engineering/standing-approved-patterns.md` — catalog source
9. V10 #2 audit-payload canonicalization research (shipyard#122) — 5-field canonical
10. V10 #1 Engineer substrate ladder specs (shipyard#121) — PR #5 foundation-idempotency follow-on
11. ADR 0092 §A3 + §A6 (uniform-404 + canonical 5-field) + ADR 0049 (audit substrate)
12. ADR 0046 (IOperationSigner — cursor/token signing)
13. fleet-conventions §SPOT-CHECK dispatch SLA

---

## 10. What ONR does next

V11 #1 deliverable complete. Proceeds to V11 #3 (Maintenance migration scope;
~30-45 min; quickest next item) per Admiral sequence #1→#3→#2→#4.

— ONR, 2026-05-22T17:30Z
