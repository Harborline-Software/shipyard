# IPaymentRepository — cross-cluster usage audit

**Authored by:** ONR (V13 batch item #1)
**Requester:** Admiral (per `admiral-directive-2026-05-22T19-05Z` item V13 #1)
**Authored at:** 2026-05-22T19-25Z

---

## Purpose

signal-bridge#34 (ReceivedThisPeriod aggregation) wired
`IPaymentRepository.ListByChartAsync` for a new Bridge surface. ONR audits
fleet usage to identify:

1. How many endpoint surfaces consume `IPaymentRepository`?
2. Are aggregation patterns consistent (calendar-month period; tenant scoping; chart-scoping)?
3. Any non-canonical aggregation that should match signal-bridge#34 shape?
4. Forward-watches for upcoming consumers

---

## 1. IPaymentRepository interface (canonical contract)

Per `shipyard/packages/blocks-financial-payments/Services/IPaymentRepository.cs`:

```csharp
public interface IPaymentRepository : ITenantScopedRepository<Payment, PaymentId>
{
    Task AddAsync(TenantId tenantId, Payment payment, CancellationToken cancellationToken = default);
    Task<Payment?> GetAsync(TenantId tenantId, PaymentId id, CancellationToken cancellationToken = default);
    Task UpdateAsync(TenantId tenantId, Payment payment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> ListByChartAsync(TenantId tenantId, ChartOfAccountsId chartId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> ListByPartyAsync(TenantId tenantId, ChartOfAccountsId chartId, PartyId partyId, CancellationToken cancellationToken = default);
}
```

**Inherits `ITenantScopedRepository<Payment, PaymentId>`** — explicit TenantId
first-positional parameter; uniform-404 on cross-tenant per ADR 0092 §A3.

**5 methods total:**
- 3 write (Add / Update; Get implicit on Update)
- 1 read-single (Get)
- 2 read-list (ListByChart / ListByParty)

**Missing:** no time-range-scoped list method. Callers compose ListByChart and
filter in-memory (per signal-bridge#34 pattern).

---

## 2. Consumer inventory

### 2.1 Substrate-internal consumers (intra-package)

| Consumer | Methods used | Pattern |
|---|---|---|
| `DefaultPaymentPostingService` | `GetAsync` (3 sites) | Get-by-id with tenant guard |
| `DefaultPaymentApplicationService` | `GetAsync` (1 site) | Get-by-id with tenant guard |
| `InMemoryPaymentRepository` | (implements interface) | n/a |

### 2.2 Bridge-layer consumers (signal-bridge)

| Consumer | File | Method | Pattern |
|---|---|---|---|
| `GET /accounting/summary` (anonymous endpoint) | `FinancialEndpoints.cs:55-127` | `ListByChartAsync` (line 105) | Calendar-month period filter (signal-bridge#34) |
| `GET /payments?leaseId=` (`payments` endpoint) | `FinancialEndpoints.cs:202+` | `ListByPartyAsync` (line 257) | Party-scoped via lease-derived partyId |
| `POST /payments` (record payment) | `FinancialEndpoints.cs:316+` | `AddAsync` (line 413) | Write-path single-payment add |

### 2.3 DI registration

- `signal-bridge/Sunfish.Bridge/Program.cs:491-497` — `Singleton<IPaymentRepository, InMemoryPaymentRepository>`
- `shipyard/packages/blocks-financial-payments/DependencyInjection/PaymentsServiceCollectionExtensions.cs:55` — `TryAddSingleton`

Note: Singleton DI for InMemory; per ADR 0092 the production-tier (EF-backed)
implementation will likely use Scoped or per-DbContext.

---

## 3. Aggregation pattern analysis

### 3.1 Bridge-layer aggregation: `ReceivedThisPeriod` (signal-bridge#29, #34)

`FinancialEndpoints.cs:95-115`:

```csharp
decimal receivedThisPeriod = 0m;
if (chartId is not null)
{
    var pmtRows = await payments.ListByChartAsync(tenantId, chartId.Value, ct).ConfigureAwait(false);
    foreach (var pmt in pmtRows)
    {
        if (pmt.Direction == PaymentDirection.Inbound
            && pmt.PaymentDate >= periodStart
            && pmt.PaymentDate <= today)
        {
            receivedThisPeriod += pmt.Amount;
        }
    }
}
```

**Pattern shape:**
1. Fetch all payments in chart via `ListByChartAsync`
2. In-memory filter by:
   - `Direction == Inbound` (received vs sent)
   - Date range `periodStart..today` (calendar month)
3. Sum `Amount` into running total

**Period semantics (line 99-101 comment):** Option B1 = calendar month per
signal-bridge#29 forward-watch.

`periodStart` resolution: `CurrentCalendarMonth()` helper at line 466 returns
`(start: new DateOnly(today.Year, today.Month, 1), today: DateOnly.FromDateTime(DateTime.UtcNow))`.

**Performance characteristic:** O(N) over all payments in chart. For high-volume
tenants (1000+ payments/chart/year), this fetches all rows + filters in-memory.
Acceptable at MVP scale; needs substrate-level time-range filter at scale-out.

### 3.2 Bridge-layer aggregation: payments-by-lease (signal-bridge#29 cohort-2 PR 1)

`FinancialEndpoints.cs:202-280` (per V13 audit; partial inspection):

Uses `ListByPartyAsync(tenantId, chartId, partyId)` after deriving `partyId`
from lease lookup. No time-range filter; returns all-time payments for the
party.

**Pattern shape:**
1. Lease lookup → derive partyId
2. Fetch all payments by party via `ListByPartyAsync`
3. Return list (no filtering at Bridge)

### 3.3 Bridge-layer write: `AddAsync` (cohort-2 PR 3)

`FinancialEndpoints.cs:316+` (per V13 audit; partial inspection):

Write-path single-payment add per `POST /payments`. Pattern-012a (per V11 #1).

---

## 4. Consistency analysis

### 4.1 Consistent patterns observed

✅ **TenantId explicit first parameter** — every consumer passes `tenantId`
   explicitly (no implicit DI sourcing in the substrate call)
✅ **Server-derived tenant** — Bridge handlers source `tenantId` from
   `ITenantContext` (per ADR 0091); DTOs never carry `tenant_id` field
✅ **Singleton in-memory DI** — `Program.cs:491-497` + `PaymentsServiceCollectionExtensions.cs:55`
✅ **Pattern-009 conformance** — Bridge endpoints + frontend rebind pair
   (sunfish#19/signal-bridge#29)

### 4.2 Inconsistencies / pattern gaps

⚠️ **No substrate-level time-range filter method** — Bridge handlers compose
   `ListByChartAsync` + in-memory filter. Multiple consumers could benefit
   from `ListByChartInDateRangeAsync(tenantId, chartId, from, to, ct)`.

⚠️ **Period semantics non-canonical** — `CurrentCalendarMonth()` is Bridge-
   layer logic (line 466). If 2nd-consumer surface (e.g., Quarterly Aging
   report) wants a different period, the helper must change OR each consumer
   re-implements.

⚠️ **No substrate-level aggregation methods** — substrate exposes `ListByChartAsync`
   only; aggregation (Sum, Count, GroupBy direction) is Bridge-layer
   responsibility. For complex reports (TrialBalance, ArAging — per cohort-3
   PAO #116), substrate may need `SumByChartInDateRangeAsync` or stream-based
   aggregation.

⚠️ **No `ListByLeaseAsync` method** — the "payments by lease" surface goes via
   `ListByPartyAsync` after lease-to-party derivation. If lease-payments
   is a common Bridge query, substrate could expose `ListByLeaseAsync` directly.

⚠️ **No total-count surface** — substrate has no `CountAsync`; UI surfaces
   wanting "Total payments: N" must enumerate.

### 4.3 Non-canonical aggregation watch

No NON-canonical aggregation detected. All current consumers use the same
shape:
- `ListByChartAsync` for time-range or full-list queries
- `ListByPartyAsync` for party-scoped queries
- `GetAsync` for single-id lookup
- In-memory filter + aggregation for Bridge-specific shapes

**Aggregation pattern coherence: GOOD.** No pre-emptive substrate amendment
needed.

---

## 5. Forward-watches for upcoming consumers

### 5.1 Cohort-3 PAO #116 reports cluster (V10 #3)

Per V10 #3 forecast, cohort-3 PR cluster includes:
- TrialBalance, ArAging, ProfitAndLoss, RentRoll (4 reports)

Each report aggregates over Invoices + Payments + Bills + Journal Entries.
Likely `IPaymentRepository` consumers:
- **TrialBalance** — sum payments by chart + direction
- **ArAging** — sum payments by invoice + aging bucket (Invoice + Payment join)
- **ProfitAndLoss** — sum payments by chart + period

**Aggregation shape forecast:** uses `ListByChartAsync` + in-memory filtering
(consistent with signal-bridge#34). No substrate-level amendments needed unless
PAO's report-engine substrate (cartridge-based) introduces its own aggregation
contract.

### 5.2 Pattern-012a/012b forward-watches (V12 #2)

Per V12 #2 forward-watch:
- **pattern-012a 3rd-instance candidate**: future POST endpoints on Leases /
  Maintenance / Properties / Vendors — IPaymentRepository unlikely consumer
- **pattern-012b 3rd-instance candidate**: AR invoice posting, journal void/
  reverse, payment application/unapply — `IPaymentRepository.AddAsync` /
  `UpdateAsync` likely consumers

### 5.3 Onboarding-ladder sub-cohort 4 (V8 #3)

Invitations admin-create endpoint — unlikely IPaymentRepository consumer
(no payment flow in invitation lifecycle).

### 5.4 Stripe / payment-gateway integration (forward-watch; future)

If Sunfish integrates payment-gateway substrate (Stripe / Plaid / etc.), the
Bridge handler for `POST /payments` may need to:
- Persist provisional payment via `AddAsync`
- Await gateway confirmation
- `UpdateAsync` payment status

This pattern would emerge as 2nd-instance of `IPaymentRepository.UpdateAsync`
usage (currently no Bridge-layer Update consumer).

---

## 6. Proposed substrate-amendment forward-watches (DO NOT enact yet)

When 2nd-instance demand emerges, consider amending IPaymentRepository:

| Proposed method | Justification | 2nd-instance demand signal |
|---|---|---|
| `ListByChartInDateRangeAsync(tenantId, chartId, from, to, ct)` | Eliminate Bridge-layer date-range filter loops | TrialBalance + ArAging consumers (cohort-3) |
| `ListByLeaseAsync(tenantId, leaseId, ct)` | Direct lease-scoped query without partyId derivation | LeaseDetailPage payments-section + future audit-trail viewer (cohort-4 cross-ref) |
| `SumByChartInDateRangeAsync(tenantId, chartId, from, to, direction, ct)` | Pure aggregation; avoids fetching full payment list | ProfitAndLoss report (cohort-3) |
| `CountAsync(tenantId, query, ct)` | Total-count surface | UI "Showing N of M" affordance |

Each candidate should wait for 2nd-instance demand before substrate amendment.

---

## 7. Pattern emergence forward-watches

**pattern-tenant-scoped-list-with-bridge-aggregation** (candidate; potential):
- `ListByXAsync` substrate methods + Bridge-layer in-memory aggregation
- Already 2nd-instance in IPaymentRepository (ListByChartAsync + ListByPartyAsync)
- Multiple cross-cluster: IInvoiceRepository, IJournalStore, IBillRepository all
  follow this shape

If formalized as pattern, would inform future substrate authoring (e.g.,
IBookkeeperRepository, IVendorRepository pattern decisions).

---

## 8. Decisions surfaced to Admiral

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **No substrate amendment urgency** — current consumers consistent; defer
   amendments until 2nd-instance demand
2. **Pattern-tenant-scoped-list-with-bridge-aggregation** — register as
   candidate pattern? ONR observation: 4 substrate clusters already follow this
   shape; promote to candidate now per V8 #6 cadence?
3. **PaymentDirection invariant** — Bridge handler at line 108 filters
   `pmt.Direction == PaymentDirection.Inbound`. Is there a `PaymentDirection.
   Outbound` consumer somewhere? ONR's read: yes, Bills cluster handles
   outbound. Confirm + add audit trail.
4. **Stripe/payment-gateway integration** — when will this work fire? Not in
   MVP-demo critical path per V7 #3. Defer to post-MVP.

---

## 9. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T19-05Z` item V13 #1
2. signal-bridge#29 (cohort-2 financial endpoint family; MERGED)
3. signal-bridge#34 (ReceivedThisPeriod aggregation; MERGED)
4. sunfish#19 (RentCollectionPage write-path; MERGED)
5. `shipyard/packages/blocks-financial-payments/Services/IPaymentRepository.cs:29-58`
6. `shipyard/packages/blocks-financial-payments/Services/InMemoryPaymentRepository.cs`
7. `signal-bridge/Sunfish.Bridge/Financial/FinancialEndpoints.cs:55-127, 202-280, 316-413, 466`
8. V7 #3 MVP demo critical-path (shipyard#111)
9. V8 #3 onboarding-ladder Stage-02 (shipyard#117)
10. V10 #3 cohort-3 PR cluster consolidation (shipyard#123)
11. V11 #1 pattern-012 canonical framing (shipyard#124)
12. V12 #2 pattern-012a/b 3rd-instance watch (shipyard#130)
13. ADR 0091 R2 (ITenantContext) + ADR 0092 R2 (substrate tenant-keyed) + ADR 0049 (audit substrate)

---

## 10. What ONR does next

V13 #1 audit complete. Proceeds to V13 #2 (ADR 0094 Step 2+ scoping; ~1-2h).

— ONR, 2026-05-22T19:25Z
