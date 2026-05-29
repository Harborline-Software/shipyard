# Engineer V3 #1 — progress tracking

**Authored by:** ONR (V13 batch item #4)
**Requester:** Admiral (per `admiral-directive-2026-05-22T19-05Z` item V13 #4)
**Authored at:** 2026-05-22T19-15Z

---

## TL;DR — CRITICAL FINDING

**Engineer V3 #1 already SHIPPED as shipyard#100 — MERGED 2026-05-21T13:51Z.**

The PR `feat(kernel-audit): IAuditEventReader Step 1-5 substrate primitives +
InMemoryAuditEventReader + DI + tests (ADR 0094)` (shipyard#100) implements
ADR 0094 Steps 1-5 in `shipyard/packages/kernel-audit/`. It merged
**~3 hours before** Admiral's V3 dispatch directive 2026-05-22T16:30Z asked
Engineer to author "shipyard Step 1 IAuditEventReader kernel-audit package
(~280 LOC; HIGHEST PRIORITY)".

This means ONR's V10 #1 (16:30Z) + V11 #2 (17:55Z) + V12 #3 (18:55Z) spec
work was authoring a "future" PR that had ALREADY shipped. The specs remain
useful as canonical reference for the next consumer of the package (signal-
bridge audit-events endpoint family) but the kernel-audit package itself is
LIVE on main.

---

## 1. shipyard#100 actual state vs ONR specs

### 1.1 Match: API surface aligns with V12 #3 supplement

Engineer's actual `IAuditEventReader` interface in
`packages/kernel-audit/IAuditEventReader.cs` lines 79-138:

```csharp
public interface IAuditEventReader
{
    Task<AuditRecord?> GetByIdAsync(
        TenantId tenantId,                  // EXPLICIT first positional ✓
        Guid auditId,
        CancellationToken ct = default);

    Task<AuditEventPage> ListAsync(
        TenantId tenantId,                  // EXPLICIT first positional ✓
        AuditEventReaderQuery query,
        CancellationToken ct = default);

    IAsyncEnumerable<AuditRecord> StreamAsync(
        TenantId tenantId,                  // EXPLICIT first positional ✓
        AuditEventReaderQuery query,
        CancellationToken ct = default);
}
```

All 3 invariants from V12 #3 supplement §2 match Engineer's implementation:
- Explicit TenantId first positional ✓
- Method names: `GetByIdAsync` / `ListAsync` / `StreamAsync` ✓
- Uniform-empty cross-tenant per ADR 0092 §A3 ✓
- Emission via write-side IAuditTrail (no self-emit) ✓
- 5-field canonical payload per ADR 0094 §Decision drivers ✓
- No per-row emission on List/Stream (ADR 0092 §A6) ✓
- Reverse-chronological order (OccurredAt DESC, AuditId DESC) ✓
- PageSize ignored by StreamAsync ✓

### 1.2 Naming divergence: `AuditEventReaderQuery` (not `AuditEventQuery`)

V12 #3 supplement §1 named the filter type `AuditEventQuery`. Engineer's
actual code uses **`AuditEventReaderQuery`** to disambiguate from the
existing `AuditQuery` type (ADR 0049's kernel-internal stream filter).

**V12 #3 amendment needed:** rename references from `AuditEventQuery` →
`AuditEventReaderQuery`. ONR will file V13/V14+ correction PR amending
shipyard#131 + shipyard#127 to use Engineer's canonical naming.

This is a minor naming improvement — Engineer's choice is **better**
than V12 #3's because it distinguishes the two query types semantically.

### 1.3 Cursor signing — punted to Engineer follow-on PR

shipyard#100 §"Forward-watch items (Step 5)" punted
`EventLogBackedAuditEventReader` to a follow-on PR. The in-memory
implementation uses unsigned cursor (acceptable for in-memory tests + dev).

**Production cursor signing** (per V11 #2 §5.4 + V12 #3 §1) will land in
either:
- **Step 6 (EventLogBackedAuditEventReader)** — substrate Step 6 follow-on
- **Bridge endpoint family** — signal-bridge audit-events Engineer PR 0

Per V11 #2 + V12 #3 recommendation: **Bridge layer signs**. Substrate
returns/receives opaque blob (no signature awareness in substrate).
shipyard#100 §"AuditEventCursor" already shapes cursor as tenant-bound +
opaque; signing layer flexibility preserved.

### 1.4 Forward-watches Engineer flagged in shipyard#100

**Engineer FW1 — Audit-payload field-count canonicalization (sec-eng).**
Engineer notes "ADR 0092 §A6 currently enumerates a 4-field shape;
InMemoryMaintenanceService ships a 3-field shape." — This is the exact
same finding ONR surfaced in V10 #2 (shipyard#122) + V11 #3 (shipyard#125)
migration scope.

**Implication:** Engineer's shipyard#100 was authored BEFORE ONR V10 #2;
both arrived at same finding independently. Validates the canonical 5-field
shape AND validates the V11 #3 migration scope (Engineer + ONR converge).

**Engineer FW2 — DI lifetime assertion mechanism (ADR 0094 Amendment 2.5).**
Engineer notes `ValidateInMemoryAuditTrailLifetime()` is currently test-only;
future hygiene PR could elevate to runtime `IStartupValidator`. Aligns with
V8 #4 ADR 0093 Rev 2 scaffolding §C "FAILED conditions / kill triggers"
direction (runtime invariant assertions).

### 1.5 Pattern claim in shipyard#100

Engineer's PR description:

```
@candidate-pattern: pattern-012 — Kernel-tier read-side interface + InMemory
reference implementation (new substrate surface shape: interface + cursor-
based pagination + tenant-boundary-violation emission via write-side + DI
Scoped lifetime assertion). Candidate pending 3 shippings.
```

**CRITICAL FINDING #2:** This is a **THIRD distinct pattern-012 claim**.
Adds to V11 #1 inventory:

- Instance A (pattern-012a per V11 #1): sunfish#19 + signal-bridge#29 PAIR
  — financial-write-path with separated CSRF
- Instance B (pattern-012b per V11 #1): signal-bridge#37 — accountant-grade
  write-path with inlined CSRF + idempotency
- **Instance C (NEW; shipyard#100): kernel-tier read-side interface +
  InMemory reference implementation** — fundamentally different shape from
  A and B (kernel-tier substrate not Bridge endpoint)

Per V11 #1 catalog-framing analysis, Instance C does NOT fit pattern-012a
OR pattern-012b. It's a **fourth shape entirely** — possibly:
- **pattern-012c-kernel-tier-read-substrate** (new sub-pattern)
- OR rename pattern-012 entirely (deprecate the financial-write-path naming)
- OR move shipyard#100 to a new pattern series (pattern-018+)

**ONR recommendation:** route to Admiral as V13 question. Pattern-catalog
framing now has THREE distinct claims at "pattern-012", confirming V11 #1's
RED-flag was even more severe than initially documented.

---

## 2. Has Engineer started V3 #1?

**Technical answer: YES, V3 #1 = shipyard#100 (already MERGED).**

**Operational answer: Engineer V3 #1 status check needed.**

Admiral's V3 dispatch directive 2026-05-22T16:30Z may have:
- Been unaware shipyard#100 already merged (status-cache miss)
- OR was about EventLogBackedAuditEventReader (Step 6 follow-on punted in shipyard#100)
- OR was about signal-bridge audit-events Bridge endpoint family (next consumer)

ONR routes to Admiral for clarification (per `feedback_onr_questions_via_inbox`).

---

## 3. Engineer V3 #1 deviations from V12 #3 supplement

Since shipyard#100 SHIPPED BEFORE V12 #3 supplement, "deviations" reframed
as "what V12 #3 should be updated to match":

| Aspect | V12 #3 supplement | shipyard#100 actual | Action |
|---|---|---|---|
| Query type name | `AuditEventQuery` | `AuditEventReaderQuery` | V12 #3 amend |
| Interface | `IAuditEventReader` | `IAuditEventReader` | ✓ match |
| TenantId param | EXPLICIT first | EXPLICIT first | ✓ match |
| Method names | GetByIdAsync / ListAsync / StreamAsync | GetByIdAsync / ListAsync / StreamAsync | ✓ match |
| Uniform-empty | yes | yes | ✓ match |
| TBV emission | write-side IAuditTrail | write-side IAuditTrail | ✓ match |
| 5-field payload | canonical | canonical | ✓ match |
| Cursor type | opaque string | `AuditEventCursor` record | minor; V12 #3 amend |
| DI extension | `AddSunfishKernelAudit()` | `AddSunfishKernelAuditReaderInMemory()` | V12 #3 amend |
| DI lifetime | Singleton (V12 #3) | Scoped per ADR 0092 | **V12 #3 amend** |
| Test count | 14 (V12 #3 §5) | 22 (8 existing + 8 new + FW1/FW2) | V12 #3 amend |
| `Snapshot()` accessor | not mentioned | added to InMemoryAuditTrail | V12 #3 amend |

**Substantive divergence:** V12 #3 supplement §4 specified Singleton DI lifetime;
Engineer used Scoped per ADR 0092. ADR 0092 is the canonical authority on
substrate DI lifetimes — Engineer's choice is correct. V12 #3 amendment
needed.

### 3.1 Amendment PR scope

ONR drafts amendment PR to shipyard#131 (V12 #3) updating:
1. Rename `AuditEventQuery` → `AuditEventReaderQuery` throughout
2. Update DI lifetime from Singleton → Scoped per ADR 0092
3. Rename DI extension from `AddSunfishKernelAudit()` → `AddSunfishKernelAuditReaderInMemory()`
4. Update test count expectation from 14 → 22 (Engineer's actual)
5. Mark V12 #3 as "POST-FACTUM consultation" rather than "supplementary spec"
  (since shipyard#100 already shipped)
6. Add note: V12 #3 remains useful as canonical reference for future consumers

**Estimated:** ~30-45 min ONR work; ~50-80 LOC delta on shipyard#131

---

## 4. ONR clarifications offered to Engineer

Per V13 #4 directive: "If Engineer asks clarifying questions: ONR answers from
research base."

ONR proactively offers these clarifications based on shipyard#100 ↔ ONR
spec drift:

### 4.1 If Engineer asks: "What is the canonical pattern-012 split now?"

Per V11 #1 (shipyard#124) Option B + V13 #4 finding:
- **pattern-012a** = separated CSRF + no idempotency (sunfish#19 + signal-bridge#29)
- **pattern-012b** = inlined CSRF + mandatory Idempotency-Key (signal-bridge#37)
- **pattern-012c (proposed)** = kernel-tier read-side substrate (shipyard#100)
- All await Admiral ratification (V8 #3 + V11 #1 + V13 #4 questions outstanding)

### 4.2 If Engineer asks: "Should shipyard#100's pattern claim be updated?"

**Yes, eventually.** When Admiral ratifies the split, Engineer (or QM) files
amendment PR to shipyard#100's PR description updating
`@candidate-pattern: pattern-012` → `@candidate-pattern: pattern-012c-kernel-tier-read-substrate`.

shipyard#100 already merged; PR description is non-load-bearing post-merge,
but updating for accuracy is recommended (consistency with V11 #1 ruling).

### 4.3 If Engineer asks: "What's the next step?"

Per shipyard#100 forward-watches + V13 #2 ADR 0094 Step 2+ scoping
(forthcoming):
- **EventLogBackedAuditEventReader** (Step 6) — production implementation
  layering over `IEventLog.ReplayAsync`
- **signal-bridge audit-events Bridge endpoint family** — consumer of
  shipyard#100; cohort-4 PR 0 per V9 #1 spec

Both can run in parallel; signal-bridge endpoint family is more time-critical
(cohort-4 demo path).

### 4.4 If Engineer asks: "How does cursor signing work?"

Per V11 #2 §5.4 + V12 #3 §1:
- **Substrate (shipyard#100)**: returns/receives opaque blob; no signing awareness
- **Bridge layer**: signs cursor via `IOperationSigner.SignAsync` before returning
  to client; verifies signature on incoming cursor before calling substrate
- **Engineer's cohort-4 Bridge endpoint PR** will implement Bridge-layer signing

---

## 5. Forward-watches for Engineer V3 #2+ (next steps)

Based on shipyard#100 forward-watches + cohort-4 sequence:

1. **EventLogBackedAuditEventReader** (~1-2 days; ADR 0094 Step 6 per shipyard#100 framing)
2. **signal-bridge `/api/v1/audit-events*` Bridge endpoint family** (~1-2 days;
  consumer of shipyard#100; per V9 #1 spec)
3. **InMemoryMaintenanceService 5-field canonical migration** (~30-45 min Engineer;
  per V11 #3 scope shipyard#125)
4. **ADR 0092 §A6 mini-amendment** (~Admiral authors; per V10 #2 shipyard#122 §4.1 scaffold)

ONR forward-watches each post-V13.

---

## 6. Decisions surfaced to Admiral

For Admiral routing per `feedback_onr_questions_via_inbox`:

1. **shipyard#100 status awareness** — Was Admiral aware shipyard#100 was already
  MERGED when V3 dispatch directive issued 2026-05-22T16:30Z? Status-cache miss?
  Or V3 #1 actually meant EventLogBackedAuditEventReader (Step 6)?
2. **shipyard#100 pattern claim update** — Engineer (or QM) files amendment PR
  updating `@candidate-pattern: pattern-012` → `pattern-012c-kernel-tier-read-
  substrate` post Admiral ratification of split?
3. **pattern-012c proposal** — Adopt pattern-012c-kernel-tier-read-substrate
  as 3rd sub-pattern in the V11 #1 split? Or rename pattern-012 series entirely?
  ONR recommends pattern-012c (lettered) for consistency with V11 #1 -012a / -012b.
4. **V12 #3 amendment urgency** — File ONR amendment PR to shipyard#131 now,
  OR wait until next consumer needs the canonical reference?
  ONR recommends now (~30-45 min ONR work).

---

## 7. Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T19-05Z` item V13 #4
2. `coordination/inbox/admiral-directive-2026-05-22T16-30Z-engineer-v3-batch-cohort-4-prep.md` — Engineer V3 dispatch directive
3. shipyard#100 (MERGED 2026-05-21T13:51Z; `feat(kernel-audit): IAuditEventReader Step 1-5`)
4. `shipyard/packages/kernel-audit/IAuditEventReader.cs:79-138` — actual interface
5. V10 #1 Engineer substrate ladder (shipyard#121) §4 — initial spec (PRE-shipyard#100 awareness)
6. V10 #2 audit-payload canonical (shipyard#122) — 5-field convergent finding
7. V11 #1 pattern-012 canonical framing (shipyard#124) — original split
8. V11 #2 ADR 0094 Engineer consultation (shipyard#127) — initial canonical alignment
9. V11 #3 Maintenance migration scope (shipyard#125) — convergent FW1 finding
10. V12 #3 Engineer V3 #1 supplement (shipyard#131) — canonical reference (needs amendment per §3)
11. ADR 0094 (Accepted 2026-05-21)
12. ADR 0092 (substrate tenant-keyed; Scoped DI lifetime authority)
13. fleet-conventions §SPOT-CHECK dispatch SLA

---

## 8. What ONR does next

V13 #4 progress-tracking complete. Proceeds to V13 #1 (IPaymentRepository
cross-cluster usage audit; ~1-2h).

— ONR, 2026-05-22T19:15Z
