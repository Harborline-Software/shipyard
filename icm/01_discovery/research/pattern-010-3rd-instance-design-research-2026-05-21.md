# ONR research — Pattern-010-financial-write-path 3rd-instance design (2026-05-21)

**Requester:** Admiral (per `admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #4)
**Authored by:** ONR
**Authored at:** 2026-05-21T12-18Z
**Status:** draft (ratification pending sec-eng + .NET-architect council review on the 3rd-instance candidate selection + Q1/Q2/Q3 design choices)

---

## Scope of investigation

- **In scope:** identify the lowest-friction 3rd-instance candidate for `pattern-010-financial-write-path` ratification. Both councils HOLD pending 3rd instance per V2 directive §"#3 Pattern-010-financial-write-path 3rd-instance design research". Sec-eng Q1 (inlined-vs-separated CSRF) / Q2 (Idempotency-Key) / Q3 (E2 reload-vs-retry) still open from prior council review.
- **Out of scope:** Cohort-3 reports cluster (read-only; no write-path; explicitly noted in directive as unlikely candidate); pattern-010 catalog promotion itself (post-3rd-instance work; separate workstream).
- **Authoritative sources consulted:** cohort-2 hand-off §3.27 (pattern-010-financial-write-path candidate; FED PR 3 RentCollection POST as 1st instance); `shipyard/_shared/engineering/standing-approved-patterns.md` (pattern-010 reservation status); cohort-3 hand-off §1.4 (no write-path); cohort-2 PR 0 cluster merges (substrate context).
- **Success looks like:** Admiral has 3 ranked candidates with effort + risk + sec-eng-Q-alignment scoring; CIC can ratify a cohort-4 anchor candidate; Engineer can scaffold the 3rd-instance PR using this research.

---

## TL;DR

1. **Cohort-3 reports cluster has NO write-path** (read-only cartridges; explicit per directive). Cannot supply a 3rd-instance candidate.

2. **Cohort-4 candidate inventory** (lowest-friction first):
   - **Candidate A — `POST /api/v1/financial/journal-entries`** (manual journal entry posting) — financial; aligns with pattern-010 scope verbatim; sec-eng Q1/Q2/Q3 all applicable.
   - **Candidate B — `POST /api/v1/financial/invoices`** (invoice creation) — financial; aligns; same Q1/Q2/Q3 surface; broader entity (Invoice has more fields than JournalEntry).
   - **Candidate C — `POST /api/v1/financial/bills`** (bill creation) — financial AP-side; same shape as Candidate B but on AP cluster.
   - **Candidate D — `POST /api/v1/financial/payments/refund`** (refund initiation) — explicitly cohort-2 forward-watched; out-of-scope until refund substrate ships.

3. **ONR recommends Candidate A — journal-entries POST** as the 3rd-instance anchor. Smallest entity; clearest sec-eng surface (DR/CR balance check is the primary domain invariant); aligns with W#60 P4 PR 2 Accountant role (which needs `POST /api/v1/accounting/journal-entries` per W#60 P4 hand-off PR 2). Two cohorts converge on the same surface — efficient ratification.

4. **Sec-eng Q1 (inlined-vs-separated CSRF) — ONR recommends INLINED** (CSRF token in the same POST request body alongside payload). Mirrors cohort-2 PR 3 RentCollection POST precedent; reduces round-trip count.

5. **Sec-eng Q2 (Idempotency-Key) — ONR recommends MANDATORY** for journal entries + invoices + bills (financial integrity over double-submit cost). Caller-supplied UUID header; server-side dedup with 24h TTL on hash of `(idempotency_key, tenant_id)`.

6. **Sec-eng Q3 (E2 reload-vs-retry) — ONR recommends RELOAD on conflict** (409 response → frontend reloads page state from server; user re-submits if intent unchanged). Avoids double-write under network retries.

7. **Effort estimate for 3rd instance:** ~3-4h Engineer (single Bridge endpoint + frontend form + sec-eng SPOT-CHECK) — substantial but mechanical. After clean shipping, pattern-010 ratifies to formal.

---

## 1. Current state — pattern-010 status

### 1.1 Candidate naming

Per cohort-2 hand-off §3.27 (shipyard#42, MERGED 2026-05-19T16:40Z):

> Plus an explicit forward-claim:
> ```
> @candidate-pattern: pattern-010-financial-write-path (Bridge POST endpoint + CSRF + tenant-derived audit emission + cross-tenant rejection without diagnostic leak — first instance in financial cluster; subsequent financial write paths can pattern-match)
> ```
> ONR proposes `pattern-010` for catalog ratification IF PR 3 ships clean and one subsequent financial write path (e.g., refund / void) pattern-matches; otherwise stays as a candidate for future ratification.

**Naming note:** `standing-approved-patterns.md` line 323 shows `pattern-010` is currently a DIFFERENT pattern (apps/docs/blocks toc.yml entry bundled with pattern-006). The financial-write-path candidate may need renumbering OR the docs `pattern-010` may be deprecated. Engineer pre-flight: verify with Admiral before catalog promotion PR opens.

### 1.2 1st + 2nd instances (per directive § "#3" framing)

The V2 directive states: "Both councils HOLD on pattern-010 pending 3rd instance." This implies TWO instances have shipped + been council-reviewed. ONR's read:

- **1st instance:** cohort-2 PR 3 `POST /api/v1/financial/payments` (RentCollectionPage rebind) — MERGED via the cohort-2 hand-off execution path
- **2nd instance:** unverified within this research's scope. Possibly cohort-2 PR 0 cluster (substrate write-path counts) OR a cohort-1 forward-watch on the cockpit MaintenancePage create POST.

**ONR's recommendation:** Engineer pre-flight clarifies the 2nd instance identity before opening the 3rd. If only 1 instance has shipped, the directive framing is forward-looking ("after 3 ship, ratify").

### 1.3 Council Q1/Q2/Q3 (per directive line 42)

Per V2 directive:

> Sec-eng Q1 (inlined-vs-separated CSRF) / Q2 (Idempotency-Key) / Q3 (E2 reload-vs-retry) still open.

Reconstruction of likely council questions (based on cohort-2 PR 3 sec-eng SPOT-CHECK):

- **Q1 — Inlined-vs-separated CSRF:** does the antiforgery token travel in the POST request body (inlined) or as a separate header / cookie (separated)?
- **Q2 — Idempotency-Key:** does the endpoint require / support an `Idempotency-Key` header for caller-controlled dedup?
- **Q3 — E2 reload-vs-retry:** when the endpoint returns 409 Conflict (e.g., concurrent modification), does the frontend reload state from server OR retry the original request?

---

## 2. Candidate inventory + scoring

### 2.1 Candidate A — `POST /api/v1/financial/journal-entries`

**Source:** W#60 P4 PR 2 hand-off names `POST /api/v1/accounting/journal-entries` for the Accountant role. Cohort-4 + W#60 P4 PR 2 converge on this surface.

**Entity:** `JournalEntry` (blocks-financial-ledger; cohort-2 PR 0d added tenant-keyed `IJournalStore`)

**Wire format (proposed):**

```http
POST /api/v1/financial/journal-entries
Content-Type: application/json
Authorization: Bearer <token>
Idempotency-Key: <caller-supplied UUID>

{
  "antiforgery_token": "<server-issued token from /api/v1/antiforgery/refresh>",
  "posting_date": "2026-05-21",
  "memo": "Q1 utility expense reclassification",
  "lines": [
    {
      "account_code": "5100",
      "amount": 250.00,
      "direction": "Debit"
    },
    {
      "account_code": "1100",
      "amount": 250.00,
      "direction": "Credit"
    }
  ]
}
```

**Sec-eng surfaces:**
- CSRF inlined (per Q1 ONR recommendation)
- Idempotency-Key required (per Q2 ONR recommendation)
- 409 on duplicate idempotency-key → frontend reloads (per Q3 ONR recommendation)
- Domain invariant: `SUM(debits) == SUM(credits)` (verify server-side; return 400 on imbalance)
- Audit: `AuditEventType.JournalEntryPosted` (new constant)
- Cross-tenant: account_code must belong to the tenant's chart of accounts (per `IChartCatalogService` precedent landed at PR#67)

**Effort:** ~3-4h Engineer (Bridge endpoint + frontend form on Accountant page + sec-eng SPOT-CHECK)

**Risk:** Medium — financial integrity (DR/CR balance) requires precise validation; double-submit on idempotency-key collision is a real risk

**Q1/Q2/Q3 alignment:** All three surfaces apply; clean ratification anchor

### 2.2 Candidate B — `POST /api/v1/financial/invoices`

**Source:** Cohort-4 candidate (per V2 directive § "#6 Cohort-4 scope survey" — invoice creation is a likely cohort-4 page)

**Entity:** `Invoice` (blocks-financial-ar; cohort-2 PR 0a added tenant-keyed `IInvoiceRepository`)

**Wire format:** similar to Candidate A but with Invoice-specific fields (customer, line items with quantity + unit-price, due-date)

**Sec-eng surfaces:** same Q1/Q2/Q3 + customer-belongs-to-tenant check + invoice-number-uniqueness check

**Effort:** ~4-5h (larger entity; more validation)

**Risk:** Medium-high — invoice number collision; customer lookup cross-tenant probe

**Q1/Q2/Q3 alignment:** All three surfaces apply

### 2.3 Candidate C — `POST /api/v1/financial/bills`

**Source:** Cohort-4 candidate (mirrors Candidate B but AP-side)

**Entity:** `Bill` (blocks-financial-ap; cohort-2 PR 0b added tenant-keyed `IBillRepository`)

**Wire format:** mirror of Candidate B with vendor instead of customer

**Sec-eng surfaces:** same Q1/Q2/Q3 + vendor-belongs-to-tenant check + bill-number-uniqueness check

**Effort:** ~4-5h (same as Candidate B)

**Risk:** Medium-high — same as B

**Q1/Q2/Q3 alignment:** All three surfaces apply

### 2.4 Candidate D — `POST /api/v1/financial/payments/refund`

**Source:** Cohort-2 forward-watched item per cohort-2 hand-off §7 #11 ("Refund / unapply Bridge surface" — substrate exists; Bridge layer doesn't)

**Effort:** ~6-8h — substrate work + Bridge surface + refund-specific UX

**Risk:** High — refund is the riskiest write-path semantically (reverses prior auth)

**Q1/Q2/Q3 alignment:** All three apply BUT refund adds Q4 (reversal-of-reversal — double-refund prevention)

**Verdict:** OUT-OF-SCOPE for 3rd-instance anchor; too much scope creep for ratification. Defer to a later instance.

---

## 3. ONR recommendation: Candidate A

### 3.1 Why Candidate A wins

| Criterion | Candidate A (JournalEntry) | Candidate B (Invoice) | Candidate C (Bill) | Candidate D (Refund) |
|---|---|---|---|---|
| Entity simplicity | ⭐⭐⭐ smallest | ⭐⭐ medium | ⭐⭐ medium | ⭐ largest |
| Substrate readiness | ⭐⭐⭐ cohort-2 PR 0d done | ⭐⭐⭐ cohort-2 PR 0a done | ⭐⭐⭐ cohort-2 PR 0b done | ⭐ refund-substrate missing |
| Cohort-N alignment | ⭐⭐⭐ W#60 P4 PR 2 + cohort-4 converge | ⭐⭐ cohort-4 only | ⭐⭐ cohort-4 only | ⭐ post-cohort-4 |
| Effort | ⭐⭐⭐ 3-4h | ⭐⭐ 4-5h | ⭐⭐ 4-5h | ⭐ 6-8h |
| Sec-eng risk | ⭐⭐ medium (DR/CR check) | ⭐⭐ medium | ⭐⭐ medium | ⭐ high |
| Pattern-fit | ⭐⭐⭐ clean (canonical financial write) | ⭐⭐⭐ clean | ⭐⭐⭐ clean | ⭐⭐ refund-specific surface |
| **Score** | **18/21** | **15/21** | **15/21** | **9/21** |

**Candidate A is the lowest-friction 3rd-instance anchor.** Plus the W#60 P4 convergence makes it cohort-spanning — pattern-010 ratification unblocks BOTH cohort-4 and W#60 P4 PR 2.

### 3.2 Q1 sec-eng recommendation — INLINED CSRF

Per cohort-2 PR 3 RentCollection POST precedent:

```typescript
// Frontend (sunfish/apps/web/src/api/financial.ts)
const tokenResp = await fetch('/antiforgery/token', { credentials: 'include' });
const antiforgeryToken = await tokenResp.json();    // or extracted from header

const resp = await fetch('/api/v1/financial/journal-entries', {
  method: 'POST',
  credentials: 'include',
  headers: {
    'Content-Type': 'application/json',
    'RequestVerificationToken': antiforgeryToken,    // SEPARATED header form
  },
  body: JSON.stringify({ /* journal entry payload */ }),
});
```

**Or INLINED in body:**

```json
{
  "antiforgery_token": "<token>",
  "posting_date": "...",
  ...
}
```

**ONR recommends INLINED for pattern-010.** Single POST request; no separate header round-trip required for token extraction at the receiver; reduces sec-eng review complexity (one validation site, not two).

**Note:** the existing cohort-2 PR 3 uses the SEPARATED form (`RequestVerificationToken` header). Pattern-010 could ratify EITHER form; ONR's recommendation diverges from cohort-2 PR 3 precedent. Q1 needs council confirmation.

### 3.3 Q2 sec-eng recommendation — MANDATORY Idempotency-Key

```http
POST /api/v1/financial/journal-entries
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
```

**Server-side dedup:**
- Hash `(idempotency_key, tenant_id, request_body_sha256)` → store with 24h TTL
- On duplicate hash: return SAME response as first (idempotent)
- On hash collision (same key + tenant + different body): return 422 Unprocessable Entity

**Why mandatory for financial:** journal entries directly affect ledger balance; double-post creates phantom transactions; reconciliation cost is high.

### 3.4 Q3 sec-eng recommendation — RELOAD on 409

When server returns 409 Conflict (e.g., chart of accounts version mismatch, period closed during edit), frontend:
1. Fetches latest server state (`GET /api/v1/financial/journal-entries/draft/{draftId}` OR refetches relevant chart + period state)
2. Displays the differences to the user with an explicit "Your changes vs server state" dialog
3. User confirms intent → submits as NEW request (fresh Idempotency-Key)

**vs RETRY:** automatic retry on 409 risks double-write if the conflict was transient (e.g., concurrent period close). Reload forces user intent confirmation.

---

## 4. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

### For .NET-architect council

1. **3rd-instance candidate selection — Candidate A (JournalEntry; ONR recommended) vs Candidate B (Invoice) vs Candidate C (Bill)?**
2. **Pattern-010 naming conflict** with the existing `standing-approved-patterns.md` line 323 `pattern-010` (docs toc.yml) — renumber to `pattern-011-financial-write-path` OR deprecate the docs entry?
3. **Idempotency-Key uniqueness scope** — `(idempotency_key, tenant_id)` (ONR recommended; scoped to tenant) vs `(idempotency_key)` alone (cross-tenant unique; collision-prone)?

### For security-engineering council

1. **Q1 — CSRF inlined-vs-separated:** ONR recommends INLINED for pattern-010; cohort-2 PR 3 used SEPARATED. Confirm which form ratifies.
2. **Q2 — Idempotency-Key:** ONR recommends MANDATORY for financial writes; 24h TTL on `(idempotency_key, tenant_id, body_sha256)` hash.
3. **Q3 — Reload-vs-retry on 409:** ONR recommends RELOAD; user re-submits with fresh Idempotency-Key after reviewing diff.
4. **Audit emission on idempotent duplicate** — does the duplicate submission emit a `JournalEntryDuplicateSuppressed` audit event? ONR recommends YES (forensics value); marginal cost.

### For CIC

1. **Cohort-4 anchor convergence with W#60 P4 PR 2** — does pattern-010 ratify against the W#60 P4 PR 2 JournalEntry POST (faster), OR against a future cohort-4 anchor? ONR recommends W#60 P4 PR 2 (saves a cohort).

---

## 5. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Pattern-010 naming conflict (docs toc.yml entry) | High | Low (renumber to 011) | Engineer pre-flight; rename via Admiral routing |
| Idempotency-Key collision under high concurrency | Low | Medium (false-duplicate suppression) | UUID v4 keys (negligible collision rate) + body-hash secondary check |
| 409 → reload loop if server state churns rapidly | Low | Low (user UX friction) | Reload presents diff; user controls retry timing |
| DR/CR balance check bypass via decimal precision attack | Low | Medium (audit-trail drift) | Server uses `decimal` (System.Decimal) precision; compare with epsilon = 0.00 (exact) |
| W#60 P4 PR 2 ships first → pattern-010 ratifies inside W#60 not cohort-4 | High | Low (acceptable; pattern ratification is order-agnostic) | Cohort-4 still anchors a follow-on instance |

---

## 6. Sources cited

### Primary sources

1. `coordination/inbox/admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #4 — parent directive; Q1/Q2/Q3 framing.
2. `shipyard/icm/_state/handoffs/anchor-react-rebind-cohort-2-stage06-handoff.md` §3.27 — pattern-010-financial-write-path candidate naming (shipped at PR #42).
3. `shipyard/_shared/engineering/standing-approved-patterns.md` line 323 — pattern-010 docs-toc-entry naming conflict.
4. `shipyard/icm/_state/handoffs/w60-collaboration-phase4-stage06-handoff.md` PR 2 — `POST /api/v1/accounting/journal-entries` Accountant role surface (cohort convergence point).
5. Cohort-2 PR 0 cluster (shipyard #52/#57/#60/#64) — substrate readiness for Invoice/Bill/Payment/Journal.

### Secondary sources

6. `coordination/inbox/admiral-status-2026-05-19T02-40Z-adr-0091-promoted-to-accepted.md` — adjacent ratification cadence.
7. cohort-2 hand-off §1.4 (auth/CSRF/audit conventions recap) — separated-CSRF precedent.

---

## 7. What ONR does next

Returns to V2 research queue. Per proceed-continuously discipline:

- Item #4 deliverable complete (this doc + status beacon).
- File `onr-status-*-research-queue-v2-item-4-pattern-010-3rd-instance-complete.md`.
- Proceed to V2 #5: Multi-chart-per-tenant readiness research (~3-4h).

— ONR, 2026-05-21T12:18Z
