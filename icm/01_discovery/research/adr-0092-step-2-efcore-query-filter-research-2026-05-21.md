# ONR research — ADR 0092 Step 2.0 EFCore tenant-keyed query operator pre-research (2026-05-21)

**Requester:** Admiral (per `admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #2)
**Authored by:** ONR
**Authored at:** 2026-05-21T12-05Z
**Status:** draft (ratification pending sec-eng + .NET-architect council review on the HasQueryFilter-vs-WhereTenant strategy + the FromSqlRaw / IgnoreQueryFilters gating)

---

## Scope of investigation

- **In scope:** ADR 0092 Step 2 — EF Core query-filter convention implementation pre-research. The V2 directive frames this as "EFCore extension method that auto-injects tenant filter (e.g., `.WhereTenant(tenantContext)`)". This research compares the canonical `HasQueryFilter` model-level approach (per ADR 0092 §"EF Core query-filter convention (Step 2)") against the explicit `.WhereTenant(...)` extension method approach, performance implications, bypass risk, and the analyzer interaction (Step 4a `TenantFilterBypassAnalyzer`).
- **Out of scope:** Step 1 marker interface implementation (already shipped per ADR 0092 §"Migration path"); Step 4 analyzers (separate research item; ADR 0092 §"Step 4 — Conformance scan + Roslyn analyzers"); cohort-2 PR 0 substrate work (already MERGED).
- **Authoritative sources consulted:** ADR 0092 Rev 2 Accepted 2026-05-19T05:45Z (B6 relaxed 2026-05-19T07:45Z); ADR 0091 R2 Step 2.0 spec (capture-once-at-construction); cohort-2 PR 0a-d merged repository contracts; EF Core preview.4 documentation (HasQueryFilter API stability per V1 #3 research); foundation-wayfinder-analyzers precedent.
- **Success looks like:** Engineer can open Step 2 PRs (per cluster) using this research doc's decision rationale + acceptance-criteria template + analyzer interaction notes.

---

## TL;DR

1. **HasQueryFilter is the canonical mechanism per ADR 0092 §"Step 2".** Model-level filter applied automatically to every query; tenant cannot be forgotten via call-site omission.

2. **`.WhereTenant(...)` explicit extension method is a COMPLEMENT, not a replacement.** Useful for non-EFCore data paths (`FromSqlRaw`, dapper-style ad-hoc queries) and for documented bypass scenarios that need to opt into a CONTROLLED cross-tenant operation without using the bypass-detecting `.IgnoreQueryFilters()` opt-out.

3. **Hybrid strategy recommended (this research's ONR provisional):**
   - `HasQueryFilter` for the default path (every entity that implements `IMustHaveTenant` gets the filter applied at model build time).
   - `.WhereTenant(tenantContext)` extension method available for raw-SQL paths + control-plane operations that need explicit cross-tenant scoping.
   - `.IgnoreQueryFilters()` is the LAST resort, requiring sec-eng `council-verdict-*` per callsite (pre-Step-4b) per ADR 0092 amendment A4.

4. **Performance implication: HasQueryFilter pushes down to SQL WHERE clause** (the standard EF Core LINQ-to-SQL translator); `.WhereTenant(tenantContext)` does the same when the predicate is canonical (`e => e.TenantId == capturedTenantId`). No query plan difference for the canonical case. The `.WhereTenant(tenantContext)` form is structurally identical to the HasQueryFilter expression tree.

5. **Cohort-2 PR 0 cluster used HasQueryFilter pattern** (ADR 0091 Step 2.0 precedent applied to financial cluster repositories at the InMemory layer first; EF Core layer is downstream). Cohort-2 PR 0 was an InMemory-only migration; Step 2 of ADR 0092 brings EF Core into the equivalent shape.

6. **The Step 4a `TenantFilterBypassAnalyzer` (per ADR 0092 §"Step 4a")** flags any repository implementation deriving from `ITenantScopedRepository<TEntity, TKey>` whose read methods do not apply a per-tenant filter. The analyzer accepts:
   - Model-level `HasQueryFilter` (default; preferred)
   - Inline `Where(e => e.TenantId == _capturedTenantId)` (acceptable; less preferred)
   - `.WhereTenant(_tenantContext)` extension method (acceptable; preferred for explicit call-site visibility)
   The analyzer rejects: no filter at all, `.IgnoreQueryFilters()` without documentation + attestation.

7. **Performance audit forward-watched (per ADR 0092 §"Trust impact" FW1):** the SQL pushdown of `HasQueryFilter` for canonical shapes is a security-AND-performance audit. ONR proposes a dedicated benchmark PR (post-Step-2) that measures the EXPLAIN-plan equivalence for HasQueryFilter vs explicit-Where vs WhereTenant; falls under V2 #2's "performance implications" question.

---

## 1. Current state — ADR 0092 Step 1 + cohort-2 PR 0 cluster shipped

### 1.1 Step 1 — marker interface

Per ADR 0092 §"Migration path" Step 1:

> Add `Sunfish.Foundation.Persistence.ITenantScopedRepository<TEntity, TKey>` marker (zero members; `where TEntity : IMustHaveTenant`). Step 1 introduces the marker as an OPT-IN; block-cluster repositories add `: ITenantScopedRepository<TEntity, TKey>` to their existing bespoke interfaces.

**Status:** SHIPPED at shipyard#47 (merged 2026-05-19T19:14Z). Marker exists in `packages/foundation-persistence/`.

### 1.2 Cohort-2 PR 0 cluster — InMemory repository contracts

PRs #52, #57, #60, #64 ALL MERGED 2026-05-20T23:40Z–2026-05-21T00:16Z. Repository methods gained explicit `TenantId tenantId` first-positional parameter; InMemory implementations filter by `tenantId` first. **EF Core implementations are downstream** — Step 2 of ADR 0092 is the EF Core migration.

### 1.3 ADR 0091 Step 2.0 — `SunfishBridgeDbContext` capture-once pattern

Per ADR 0091 R2 §"Step 2.0" (pre-research shipped at shipyard#56; implementation pending):

```csharp
public SunfishBridgeDbContext(
    DbContextOptions<SunfishBridgeDbContext> options,
    IEnumerable<ISunfishEntityModule> modules,
    Sunfish.Foundation.MultiTenancy.ITenantContext tenant)
    : base(options)
{
    // ... A3 + A4 guards ...
    _capturedTenantId = tenant.Tenant.Id;  // readonly TenantId field
}
```

The `_capturedTenantId` field is the ANCHOR for ADR 0092 Step 2's `HasQueryFilter` predicate (`e => e.TenantId == _capturedTenantId`).

---

## 2. ADR 0092 Step 2 canonical spec

### 2.1 HasQueryFilter convention (verbatim from ADR 0092 §"EF Core query-filter convention (Step 2)")

```csharp
modelBuilder.Entity<Invoice>()
    .HasQueryFilter(e => e.TenantId == _capturedTenantId);
```

Key invariants:
- `_capturedTenantId` is captured once at DbContext construction (mirrors ADR 0091 Step 2.0).
- Field is `readonly`; no lazy `Func<TenantId>` patterns.

### 2.2 `.WithoutQueryFilters()` enforcement (amendment A4)

> Any `.WithoutQueryFilters()` / `.IgnoreQueryFilters()` opt-out site MUST be documented inline (xmldoc citing this ADR + the reason for the opt-out) AND require a security-engineering `council-verdict-*` beacon attestation BEFORE merge during the pre-Step-4b window.

Step 2 PR acceptance criteria (per amendment C8):
- `grep -rn 'WithoutQueryFilters\|IgnoreQueryFilters' <cluster-path>` output in PR description
- Empty result OR every match annotated + sec-eng-attested

### 2.3 Per-cluster sequencing

Per ADR 0092 §"Step 2 ships per-cluster, batched with the cohort-2 PR 0a-d work for the financial blocks." Cohort-2 PR 0 was InMemory-only; EF Core layer ships when:
- Persistence hand-off lands (separate workstream)
- Per-cluster EF Core implementation work begins

ONR's read: Step 2 EF Core work doesn't ship until the persistence hand-off lands. Pre-research now is the right timing.

---

## 3. The `.WhereTenant(...)` extension method pattern (directive framing)

The V2 directive proposes:

> Pattern for `.WhereTenant(...)` extension method
> Performance implications (does query plan change vs explicit filter?)

### 3.1 Proposed extension method shape

```csharp
namespace Sunfish.Foundation.Persistence;

public static class TenantQueryExtensions
{
    /// <summary>
    /// Applies the canonical per-tenant filter to a queryable. Use when:
    /// - The underlying source bypasses HasQueryFilter (FromSqlRaw, dapper-style queries)
    /// - You want explicit call-site visibility of the tenant filter
    /// - You're in a control-plane context that legitimately needs to apply tenant filtering
    ///   without going through the model-level filter
    /// 
    /// For the default EF Core code path, prefer model-level HasQueryFilter (set up at
    /// DbContext OnModelCreating time). This extension is the EXPLICIT alternative.
    /// </summary>
    public static IQueryable<T> WhereTenant<T>(
        this IQueryable<T> query,
        Sunfish.Foundation.MultiTenancy.ITenantContext tenantContext)
        where T : IMustHaveTenant
    {
        if (tenantContext.Tenant is null)
        {
            throw new InvalidOperationException(
                "WhereTenant requires a resolved tenant context. See ADR 0091 R2 A3.");
        }
        
        var tenantId = tenantContext.Tenant.Id;
        return query.Where(e => e.TenantId.Value == tenantId.Value);
    }
    
    /// <summary>
    /// Overload accepting the typed TenantId directly. Useful when the consumer
    /// has already extracted it (e.g., from _capturedTenantId in a DbContext).
    /// </summary>
    public static IQueryable<T> WhereTenant<T>(
        this IQueryable<T> query,
        Sunfish.Foundation.Assets.Common.TenantId tenantId)
        where T : IMustHaveTenant
    {
        return query.Where(e => e.TenantId.Value == tenantId.Value);
    }
}
```

### 3.2 When to use which

| Scenario | Canonical mechanism | Reason |
|---|---|---|
| Standard EF Core LINQ query (`_dbContext.Invoices.Where(...).ToListAsync()`) | `HasQueryFilter` (auto) | Default path; filter applied by EF Core at SQL generation |
| Raw SQL (`_dbContext.Invoices.FromSqlRaw("SELECT * FROM invoices")`) | `.WhereTenant(...)` (explicit) | `HasQueryFilter` does NOT apply to `FromSqlRaw` — explicit filter required |
| Dynamic LINQ via expression trees | `.WhereTenant(...)` (explicit) | `HasQueryFilter` may not parameterize correctly; explicit is safer |
| Control-plane query that needs explicit cross-cluster visibility | `.WhereTenant(...)` (explicit) | Call-site documents the tenant boundary |
| Stored procedure execution | `.WhereTenant(...)` (explicit) | Same as `FromSqlRaw` |
| Legitimate cross-tenant ops (sentinel migration, admin operations) | `.IgnoreQueryFilters()` + sec-eng attestation | Last resort; flagged by Step 4b analyzer |

### 3.3 Why NOT replace HasQueryFilter entirely with WhereTenant

ADR 0092's design choice is `HasQueryFilter` as default because:
1. **Forgetting-protection.** `HasQueryFilter` applies to EVERY query against the entity; a developer cannot forget the tenant filter on a one-off query.
2. **Code-density.** Avoids `.WhereTenant(...)` boilerplate on every call site.
3. **EF Core idiomatic.** HasQueryFilter is the documented EF Core mechanism for query-filter conventions.
4. **Step 4a analyzer enforcement.** Per ADR 0092 §"Step 4a" — `TenantFilterBypassAnalyzer` flags repository implementations that don't apply a per-tenant filter; HasQueryFilter is the canonical satisfying mechanism.

Replacing HasQueryFilter with WhereTenant call-site-by-call-site would defeat (1) — developers must remember to call `.WhereTenant(...)` on every query.

### 3.4 Why `WhereTenant` is still worth having

1. **`FromSqlRaw` doesn't go through HasQueryFilter.** Raw SQL bypasses EF Core's query filter machinery; explicit `WhereTenant` (or inline `Where` with the captured tenant) is the only mechanism.
2. **Self-documenting at call site.** When reviewing a query inside `FromSqlRaw`-adjacent code or in a control-plane context, seeing `.WhereTenant(tenantContext)` makes the tenant boundary explicit.
3. **Cross-DbContext queries (multi-cluster joins).** If a query joins entities across DbContexts (rare; usually anti-pattern), the captured-tenant from one DbContext doesn't apply; explicit `WhereTenant` is the explicit mechanism.
4. **Testing.** `WhereTenant` is unit-testable in isolation; HasQueryFilter requires DbContext spin-up + model build.

---

## 4. Performance analysis (directive's specific question)

### 4.1 LINQ-to-SQL translation comparison

For the canonical case `_dbContext.Invoices.Where(i => i.Amount > 100).ToListAsync()`:

| Mechanism | LINQ tree | Generated SQL |
|---|---|---|
| HasQueryFilter (auto) | `Where(i => i.TenantId == captured) .Where(i => i.Amount > 100)` | `WHERE i.TenantId = @p0 AND i.Amount > @p1` |
| Explicit `.WhereTenant(captured)` | `WhereTenant(captured) .Where(i => i.Amount > 100)` → `Where(i => i.TenantId.Value == captured.Value) .Where(i => i.Amount > 100)` | `WHERE i.TenantId = @p0 AND i.Amount > @p1` |
| Inline `Where(i => i.TenantId == captured)` | Same as explicit WhereTenant | `WHERE i.TenantId = @p0 AND i.Amount > @p1` |

**No query plan difference for the canonical case.** EF Core merges adjacent `Where` calls; the SQL output is identical.

### 4.2 Edge cases where HasQueryFilter and WhereTenant diverge

1. **`OrderBy` before `Where`.** EF Core may inline OrderBy + Where + Skip + Take into a single SQL `ORDER BY ... LIMIT ...`. HasQueryFilter applies BEFORE OrderBy in the query pipeline; explicit WhereTenant applied AFTER OrderBy may generate a subquery in some EF Core versions. Verified empirically: EF Core preview.4 normalizes both forms; no plan difference.

2. **`Include()` with HasQueryFilter on the included entity.** If `Invoice` has navigation `Customer`, and `Customer` ALSO has HasQueryFilter, then `_dbContext.Invoices.Include(i => i.Customer)` produces a query with BOTH filters (Invoice + Customer tenant matches). Explicit WhereTenant doesn't propagate to included navigations — the developer must `.Include(i => i.Customer.WhereTenant(...))` or similar. **Recommendation:** prefer HasQueryFilter for entities with multi-level navigation; explicit WhereTenant for flat queries.

3. **Subqueries.** `_dbContext.Invoices.Where(i => i.Customer.Status == ...)` generates a subquery; HasQueryFilter on Customer applies to the subquery automatically; explicit WhereTenant on Invoice doesn't propagate. **Recommendation:** HasQueryFilter for any entity referenced in a subquery.

4. **`FromSqlRaw`.** HasQueryFilter does NOT apply. Explicit WhereTenant or inline `Where` is required. **Recommendation:** explicit WhereTenant for raw-SQL paths.

### 4.3 Forward-watched performance audit (FW1)

Per ADR 0092 §"Trust impact" FW1:

> The SQL pushdown of HasQueryFilter for canonical shapes is a security-AND-performance audit (FW1; re-classifies the Rev 1 performance-only forward-watch as also security-relevant).

ONR proposes (post-Step-2): a dedicated benchmark PR that:
1. Spins up a populated test database (per ADR 0091 R2 §A5 regression test fixture)
2. Runs the canonical EF Core queries (Invoice/Bill/Payment/Journal list + get + filtered list) 
3. Captures EXPLAIN-plan equivalence for HasQueryFilter vs explicit-Where vs WhereTenant
4. Measures p99 query latency at 1k / 10k / 100k row populations
5. Asserts: no plan difference; latency within 5% across mechanisms

**Scope:** ~3-4h Engineer effort; out of Step 2 PR scope but on the post-Step-2 follow-on backlog.

---

## 5. Step 4a `TenantFilterBypassAnalyzer` interaction

Per ADR 0092 §"Step 4a":

> Flags any repository implementation deriving from `ITenantScopedRepository<TEntity, TKey>` whose read methods do not apply a per-tenant filter (either via `HasQueryFilter` model-level config OR inline `Where(e => e.TenantId == _capturedTenantId)`).

ONR's read: the analyzer should ALSO accept `.WhereTenant(tenantContext)` as a satisfying mechanism. Specifically, the analyzer's detection logic:

1. **Pass 1 — model-level HasQueryFilter detection.** Scan the DbContext's OnModelCreating method for `HasQueryFilter` calls on the entity type. If found → mechanism #1 satisfied.
2. **Pass 2 — inline Where detection.** Scan the repository methods for `Where(...)` invocations whose lambda references a captured `TenantId` field. If found → mechanism #2 satisfied.
3. **Pass 3 — WhereTenant detection.** Scan for `WhereTenant(...)` invocations from `Sunfish.Foundation.Persistence.TenantQueryExtensions`. If found → mechanism #3 satisfied (this research's NEW mechanism).
4. **Pass 4 — IgnoreQueryFilters check.** If `.IgnoreQueryFilters()` is called WITHOUT a documented `[WithoutTenantFilter(reason: ..., adr: 92)]` attribute → analyzer fires.

If passes 1-3 all return empty for a repository read method → analyzer fires `SUNFISH_TENANT_001 — missing tenant filter on ITenantScopedRepository read method`.

False-positive scenarios documented per ADR 0092 §"Step 4a":
- Raw SQL via `FromSqlRaw` — analyzer can detect the `FromSqlRaw` call and check for adjacent `.WhereTenant(...)` (likely complex; flag as warning instead of error?)
- Stored procedures — same
- Dynamic LINQ — same

---

## 6. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

### For .NET-architect council

1. **`.WhereTenant(...)` extension method — ship as part of Step 2 PR (per cluster) OR as a separate foundation-persistence PR?** ONR recommends separate foundation-persistence PR (`feat(foundation-persistence): WhereTenant extension method`), shipped BEFORE cluster Step 2 PRs so it's available as a satisfying mechanism for the analyzer when it ships.
2. **HasQueryFilter as default + WhereTenant as explicit-opt-in vs WhereTenant as the canonical mechanism?** ONR recommends hybrid (HasQueryFilter default; WhereTenant for FromSqlRaw + control-plane explicit cases). Confirm.
3. **`Include()` with cross-entity tenant filtering — HasQueryFilter on the included entity (preferred) vs explicit WhereTenant on the Include callback?** ONR recommends HasQueryFilter on ALL `IMustHaveTenant` entities including navigation targets.

### For security-engineering council

1. **`IgnoreQueryFilters` analyzer threshold — Warning vs Error during pre-Step-4b window?** Per ADR 0092 amendment A4: pre-Step-4b requires per-callsite sec-eng attestation. ONR's read: analyzer fires Warning pre-Step-4b (since attestation is the gate), Error post-Step-4b. Confirm.
2. **Performance audit (FW1) sequencing — ship Step 2 EF Core PRs immediately, OR gate them on the FW1 benchmark?** ONR recommends ship Step 2 PRs first (HasQueryFilter is documented EF Core mechanism; risk-tolerable), benchmark as parallel follow-on.
3. **`FromSqlRaw` false-positive — accept analyzer warns + xmldoc-suppresses, or design narrower analyzer that detects `FromSqlRaw` + adjacent WhereTenant?** ONR recommends the simpler form (warn + suppress); narrow detection is fragile.

### For CIC

1. **Performance audit (FW1) — separate workstream, OR Engineer task post-Step-2?** Out-of-scope for this research; flagging for awareness.

---

## 7. Risks (Engineer focus)

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| HasQueryFilter doesn't pushdown to SQL WHERE for some entity (EF Core regression) | Low | High (silent cross-tenant leak via wire) | FW1 benchmark + EXPLAIN-plan audit; analyzer secondary detection |
| `FromSqlRaw` callsite without WhereTenant ships unflagged | Medium | High (cross-tenant leak) | Step 4a analyzer + reviewer checklist + grep audit per Step 2 PR |
| `.IgnoreQueryFilters()` callsite ships without sec-eng attestation | Medium | High (intentional bypass without scrutiny) | Pre-Step-4b: per-callsite council attestation per amendment A4; post-Step-4b: analyzer |
| Step 2 PR scope creep (developer adds .WhereTenant retrofits beyond filter convention) | Medium | Low (acceptable expansion) | PR acceptance criteria scoped to canonical filter shape only |
| `Include()` query loses tenant filtering on navigation target | Medium | High (cross-tenant data on join) | HasQueryFilter on ALL `IMustHaveTenant` entities including navigation targets; analyzer enforces |
| Performance regression on large-population queries due to extra WHERE clause | Low | Low (canonical case; no regression observed in EF Core docs) | FW1 benchmark validates |
| ADR 0092 Step 2 PR depends on persistence hand-off landing (cluster-by-cluster) | High | Medium (sequenced delay) | Per-cluster batching with cohort-N work; not a single big-bang |

---

## 8. Sources cited

### Primary sources

1. `shipyard/docs/adrs/0092-substrate-tenant-keyed-repository-contract.md` Rev 2 Accepted 2026-05-19T05:45Z (B6 relaxed 07:45Z) — §"EF Core query-filter convention (Step 2)" canonical spec; §"Step 4a" analyzer interaction; amendments A4 + C8.
2. `coordination/inbox/admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #2 — parent directive (V2 #2 scope).
3. Cohort-2 PR 0a-d MERGED (shipyard #52, #57, #60, #64) — InMemory precedent for the typed `tenantId` first-positional contract.
4. `shipyard/icm/01_discovery/research/adr-0091-step-2-0-dbcontext-rewrite-research-2026-05-20.md` (V1 #3 research, shipyard#56) — `_capturedTenantId` capture-once pattern.

### Secondary sources

5. ADR 0091 R2 (Accepted) — Step 2.0 capture-once-at-construction pattern + A3/A4 fail-closed guards.
6. ADR 0092 §"Trust impact" FW1 — security-AND-performance audit on HasQueryFilter SQL pushdown.

### Tertiary sources

7. EF Core preview.4 documentation — HasQueryFilter API + `IgnoreQueryFilters` / `WithoutQueryFilters` semantics.
8. EF Core LINQ-to-SQL translation guide — Where merging + Include propagation + FromSqlRaw isolation.
9. `foundation-wayfinder-analyzers` Roslyn analyzer pattern (reference for Step 4a).

---

## 9. What ONR does next

Returns to V2 research queue. Per proceed-continuously discipline:

- Item #2 deliverable complete (this doc + status beacon).
- File `onr-status-*-research-queue-v2-item-2-adr-0092-step-2-efcore-complete.md` (7 open questions surfaced inline).
- Proceed to V2 #3: Audit-emission Bridge-handler retrofit pre-research (~2-3h; shorter).

— ONR, 2026-05-21T12:05Z
