# ADR 0091 Step 7+ scoping — does the ladder terminate at Step 6? (2026-05-21)

**Authored by:** ONR (V6 batch item #3)
**Requester:** Admiral (per `admiral-directive-2026-05-21T15-55Z` item #3)
**Authored at:** 2026-05-21T16-02Z

---

## TL;DR

**ADR 0091 substrate ladder TERMINATES at Step 6.** No Step 7+ exists in canonical ADR 0091 R2 §"Phases" OR as a logical follow-on.

ONR's reading per V5 #5 ADR 0091 Steps 5+6 pre-research (shipyard#91):

- **Step 5** = facade deletion (`Foundation.Authorization.ITenantContext` removed)
- **Step 6** = `foundation-authorization` package boundary cleanup (v2 semver bump; downstream consumer notification)

Post-Step-6, the substrate ladder has achieved:
- Decomposition complete (`Foundation.MultiTenancy.ITenantContext` + `ICurrentUser` + `IAuthorizationContext` as separate concerns)
- Facade ratchet completed (deletion + grace observed)
- Analyzer + obsolete-warning lifecycle complete (Step 4 analyzer landed; Step 5 deleted; Step 6 cleaned up)
- Package boundary clean (v2 semver; downstream consumers migrated)

**No further substrate work required.** ADR 0091 substrate matures into "shipped + stable" state.

---

## What's NOT a Step 7+

### Out-of-scope (separate ADRs / workstreams)

- **Production OIDC-impl ADR** (V1 #5 scoping research; future ADR 0XXX per `production-oidc-impl-adr-scoping-2026-05-20.md`) — different ADR; consumes ADR 0091 substrate but is NOT a step of ADR 0091
- **Foundation.Identity package migration** (V1 #5 §"Future naming-cleanup ADR") — separate ADR if/when proposed
- **Pattern-009-tenant-keying-retrofit retirement** — V4 #4 catalog audit Gap 3 candidate; retirement is YEARS out (cohort substrate retrofits complete cluster-by-cluster); not ADR 0091 step
- **Cross-cluster tenant-context propagation across the Bridge↔Anchor seam** — Phase 5 W#60 peer-sync auth concern (per V1 #5 OIDC scoping §"Out-of-scope-but-flagged"); separate Phase 5 ADR

### Forward-watched (NOT new substrate work)

ADR 0091 R2's §"Revisit triggers" section names 5 conditions that COULD trigger a Revision 3 amendment:
- Production OIDC-claims-backed impl ships (future ADR) → may surface invariants this decomposition cannot carry
- Accelerator beyond Bridge adopts `Foundation.Authorization.*` and produces per-accelerator concrete class divergence
- `Foundation.Identity` naming-cleanup ADR (O-3 future path) ships
- MUST-NOT-mix-pipelines invariant violated in production (analyzer true-positive missed at PR review)
- `SunfishBridgeDbContext` migration to per-tenant data plane (ADR 0031 Wave 5.2) supersedes typed-`TenantId` filter rewrite

None of these are Step 7+ — they're CONDITIONS for re-evaluating the ADR as a whole (Revision 3 amendment territory).

---

## ONR recommendation

**File this status as the canonical "ADR 0091 ladder terminates at Step 6" confirmation.** Future Admiral / Engineer references to "Step 7+" should redirect to:
- (a) V5 #5 ADR 0091 Steps 5+6 pre-research (canonical ladder spec)
- (b) ADR 0091 R2 §"Revisit triggers" (Revision 3 amendment conditions)
- (c) Separate ADR proposals (production OIDC, Foundation.Identity migration, etc.)

---

## Sources cited

1. `coordination/inbox/admiral-directive-2026-05-21T15-55Z` item #3
2. V5 #5 ADR 0091 Steps 5+6 pre-research (shipyard#91) — canonical ladder spec
3. ADR 0091 R2 (Accepted) §"Phases" + §"Revisit triggers"
4. V1 #5 Production OIDC-impl ADR scoping (shipyard#59) — out-of-scope separate ADR

---

## What ONR does next

V6 #3 deliverable complete. Proceeds to V6 #2 (audit-payload field count reconciliation).

— ONR, 2026-05-21T16:02Z
