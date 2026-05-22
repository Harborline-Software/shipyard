# Onboarding-ladder — 10 decisions resolution scaffold for Admiral ruling

**Authored by:** ONR (V11 batch item #4)
**Requester:** Admiral (per `admiral-directive-2026-05-22T17-15Z` item V11 #4)
**Authored at:** 2026-05-22T18-10Z
**Source:** V8 #3 onboarding-ladder Stage-02 scoping (shipyard#117) §7

---

## Purpose

V8 #3 surfaced 10 decisions for Admiral/CIC consultation on onboarding-ladder
Stage-02 scoping. Admiral will rule on these eventually; this V11 #4 scaffold
pre-drafts each decision so the Admiral ruling can be authored faster.

**Scaffold format per decision:**
- Decision name + V8 #3 reference
- Options enumerated
- Substrate / cohort / pattern implications per option
- ONR recommendation + reasoning
- CIC input gap (if any)
- Forward-watch
- Ruling shape (template Admiral consumes)

---

## Decision 1 — ADR 0095 (Onboarding Bootstrap Context) approval

**V8 #3 ref:** §7.1 + §3.4.1
**Type:** Architecture-level / new ADR

### Options

**Option A — Accept ADR 0095 as new ADR**
- Distinct substrate domain from ADR 0091 (per V8 #3 §2.1 rationale)
- ONR provides scaffold per V4 #1+#2 ADR-authoring-precedent
- Admiral authors final text

**Option B — Reject; fold into ADR 0091 Step 8+**
- ADR 0091 R2 currently spans Steps 1-7; extending to Step 8 (Bootstrap Context)
  overloads the ITenantContext divergence-resolution ADR
- ONR doesn't recommend; ADR 0091 R2 already accreting amendments (per V8 #4 Rev 2 scaffolding)

**Option C — Defer; address at cohort-onboarding Stage-05 hand-off authoring time**
- Move ADR 0095 question into the onboarding-sub-cohort 1 hand-off as a halt-condition
- Lighter; but blocks Stage-05 authoring until resolution

### Substrate implications

- **Option A**: New ADR ~3-5 days authoring + ratification cycle; unblocks onboarding
- **Option B**: ADR 0091 amendment cycle; entangles with existing divergence work
- **Option C**: Onboarding ladder Stage-05 stalls until decision

### Pattern implications

- Option A: First instance of "bootstrap-window" pattern; potential candidate
- Option B: Subsumed by ADR 0091 patterns (no new candidate)
- Option C: Deferred

### ONR recommendation

**Option A.** New ADR 0095 cleanly separates concerns; ADR 0091 R2 should stay
focused on the per-request-pipeline divergence resolution.

### CIC input gap

None; Admiral authority sufficient.

### Forward-watch

ADR 0095 amendments may emerge as additional bootstrap-context consumers are
discovered (e.g., webhook-receiver bootstrap; cross-tenant federation bootstrap).

### Ruling shape

```markdown
Decision 1 — ADR 0095 (Onboarding Bootstrap Context): **OPTION A APPROVED**.
ONR provides scaffold (forthcoming Admiral directive); Admiral authors final
ADR 0095 text. Onboarding sub-cohort 1 Stage-05 unblocked when ADR 0095 ratified.
```

---

## Decision 2 — ADR 0096 (Email Dispatch Substrate) approval

**V8 #3 ref:** §7.2 + §3.1
**Type:** Architecture-level / new ADR

### Options

**Option A — Accept ADR 0096 as new ADR**
- Net-new substrate cluster (email is fundamentally new infrastructure)
- Cross-tenant relevance (emails carry tenant context; signed links)
- ONR provides scaffold; Admiral authors final

**Option B — Reject; fold into ADR 0049 (audit substrate) or extend ADR 0046 (IOperationSigner)**
- Email substrate is conceptually distinct from audit OR signing; doesn't fit
- ONR doesn't recommend

**Option C — Defer to provider-selection ratification**
- Hold ADR 0096 until email provider chosen (Decision 4)
- Risk: provider lock-in informs interface shape; ADR drafted post-choice avoids over-design

### Substrate implications

- **Option A**: ~3-5 days authoring + ratification; substrate-level interface
- **Option C**: Holds onboarding sub-cohort 1 longer; but cleaner interface

### Pattern implications

- Option A: First instance of "provider-neutral substrate" pattern (pattern-007
  candidate hardening per V8 #3 §3.1)
- Option C: Same; just delayed

### ONR recommendation

**Option C — Defer until provider chosen.** ADR 0096 interface shape is influenced
by SendGrid vs Postmark vs SES SDK conventions. Author the ADR post-choice with
specific provider SDK constraints reflected. Loses ~1 week vs Option A but
produces cleaner interface.

If timeline pressure makes Option C unworkable, fall back to Option A with
provider-agnostic interface (likely the right shape anyway).

### CIC input gap

CIC must rule on provider selection (Decision 4) before ADR 0096 fires; gates
this decision chain.

### Forward-watch

Provider-rotation handling (how do we swap providers without ADR amendment?) —
forward-watch into ADR 0096 amendment cycle.

### Ruling shape

```markdown
Decision 2 — ADR 0096 (Email Dispatch Substrate): **OPTION C (deferred) ACCEPTED**
contingent on Decision 4 (email provider) resolving within N weeks. If Decision 4
delays beyond N weeks, escalate to Option A with provider-agnostic shape.
```

---

## Decision 3 — Self-tenant initial-write permission

**V8 #3 ref:** §7.3 + §2.2
**Type:** ADR 0092 Step addition OR new candidate pattern

### Options

**Option A — ADR 0092 Step addition (Step 7+)**
- Codify "self-tenant initial-write permission" as a Step in ADR 0092 Rev 2 or higher
- Heavy; ADR-level change for a 1-cluster (Tenant) initial concern

**Option B — Pattern-candidate (recommended)**
- Register as `pattern-self-tenant-initial-write` candidate
- Promote to ADR 0092 Step addition if 2nd-instance emerges (e.g., future "Organization" entity)
- Lighter; ratifies-via-pattern-emergence pattern

**Option C — Inline in Tenant aggregate substrate; no ratification**
- Allow Tenant aggregate to opt-out of `.WhereTenant()` filter for initial Create
- No ratified pattern; not surveyed
- Risk: undocumented exception; future entities may copy badly

### Substrate implications

- **Option A**: Heavy; locks in pattern before 2nd-instance proof
- **Option B**: Lightweight; pattern matures organically
- **Option C**: Undocumented; risky

### Pattern implications

- Option A: Patterns documented in ADR layer (heavy)
- Option B: First instance of `pattern-self-tenant-initial-write` candidate
- Option C: No pattern; technical debt

### ONR recommendation

**Option B.** Per V8 #6 pattern catalog cadence + V11 #1 sub-pattern split
precedent — lightweight emergence is the right path for novel patterns.

### CIC input gap

None.

### Forward-watch

If 2nd-instance "Organization" or "Workspace" emerges, promote to ADR 0092
Step addition.

### Ruling shape

```markdown
Decision 3 — Self-tenant initial-write permission: **OPTION B APPROVED**.
Register pattern-self-tenant-initial-write as candidate; promote to ADR 0092
Step addition if 2nd-instance emerges.
```

---

## Decision 4 — Email provider selection

**V8 #3 ref:** §7.4 + §3.1
**Type:** Commercial / CIC decision

### Options

**Option A — SendGrid**
- Mature; well-documented; .NET SDK quality high
- Pricing: $14.95/mo for 50k emails; scales to ~$600/mo at 1M emails
- Tenant-aware sandbox; webhook deliverability tracking

**Option B — Postmark**
- Premium transactional-only (no marketing emails); higher deliverability
- Pricing: $15/mo for 10k emails; $1.25 per 1k after
- Smaller .NET SDK; less mature than SendGrid

**Option C — Amazon SES**
- Cheapest at scale ($0.10 per 1k emails); AWS lock-in
- Operational complexity higher; deliverability variable until reputation built
- Good if AWS-native infrastructure (TBD per Sunfish hosting choice)

**Option D — Mailgun**
- Mid-tier; competitive pricing; .NET SDK exists
- Less popular in indie SaaS scene than SendGrid/Postmark

### Substrate implications

- All options work with the planned `IEmailDispatcher` interface
- Provider-rotation handling is forward-watch (ADR 0096)

### Pattern implications

- Provider-neutrality pattern (per V8 #3 §3.1 candidate hardening) survives across all options

### ONR recommendation

**Option B (Postmark)** for MVP-stage transactional email. Reasoning:
- Higher deliverability (premium-positioned; lower spam-folder rate)
- Pricing fine for MVP scale (10k emails covers ~50-200 tenant onboardings)
- Smaller SDK is acceptable for the IEmailDispatcher abstraction layer
- Avoids AWS lock-in (Sunfish hosting TBD)
- If volume grows past 100k emails, switch to SendGrid is straightforward via
  provider-rotation pattern

### CIC input gap

**CIC must decide.** Commercial choice; ONR provides analysis. CIC may have
existing vendor relationships, AWS commitments, or pricing-tier strategy that
informs choice.

### Forward-watch

Post-MVP: re-evaluate at 100k emails/mo OR if Postmark deliverability slips.

### Ruling shape

```markdown
Decision 4 — Email provider: **CIC ROUTES**.
ONR recommendation: Option B (Postmark) for MVP-stage. CIC final ruling pending.
```

---

## Decision 5 — CAPTCHA / anti-bot provider

**V8 #3 ref:** §7.5 + §3.5
**Type:** Commercial / CIC decision

### Options

**Option A — Cloudflare Turnstile**
- Free; privacy-respecting; lightweight (no user-puzzles by default)
- WCAG-compliant; works with screen readers
- .NET integration straightforward (REST API verify endpoint)

**Option B — hCaptcha**
- Privacy-respecting alternative to reCAPTCHA; mid-friction
- Free tier; enterprise paid
- WCAG-acceptable

**Option C — reCAPTCHA v3**
- Google-backed; mature; widely-deployed
- Privacy concerns (Google tracking); Google may rate-limit if tenant ad-spend signals
- WCAG-questionable (some accessible-bypass workarounds)

### Substrate implications

- All options integrate via Bridge-layer middleware; substrate-agnostic
- Cloudflare Turnstile is the only option that doesn't store user behavioral data

### ONR recommendation

**Option A (Cloudflare Turnstile).** Reasoning:
- Free + privacy-respecting (Sunfish positioning per the-inverted-stack book)
- WCAG-compliant by default
- Lightweight UX (no puzzles by default; only escalates on suspicion)
- Avoids Google tracking dependency

### CIC input gap

CIC ratification; ONR provides analysis.

### Forward-watch

If Turnstile fails (e.g., Cloudflare outage; high bot-pass rate), fall back to
hCaptcha. Provider-rotation pattern.

### Ruling shape

```markdown
Decision 5 — CAPTCHA / anti-bot: **CIC ROUTES**.
ONR recommendation: Option A (Cloudflare Turnstile). CIC final ruling pending.
```

---

## Decision 6 — Rate-limit substrate

**V8 #3 ref:** §7.6 + §3.4
**Type:** Architecture / .NET-architect ratification

### Options

**Option A — AspNetCore RateLimiter (built-in)**
- Ships with .NET 11; mature in .NET 7+
- Token-bucket + fixed-window + sliding-window built-in
- In-memory by default; Redis adapter via `Microsoft.AspNetCore.RateLimiting.Redis`

**Option B — Custom AspNetCore middleware**
- Full control; can use IDistributedCache for backing
- More code to maintain; reinvents the wheel

**Option C — Third-party (e.g., AspNetCoreRateLimit NuGet)**
- Mature; large feature set
- Adds dependency; less control than custom

### Substrate implications

- All options work at Bridge middleware layer; substrate-agnostic

### ONR recommendation

**Option A (AspNetCore built-in RateLimiter).** Reasoning:
- Native to .NET 11 (Sunfish target framework)
- Token-bucket fits "burst of signups followed by per-tenant rate" pattern
- Redis backing-store available when horizontal-scaling matters
- No new dependency

### CIC input gap

None; .NET-architect ratification sufficient.

### Forward-watch

Horizontal scaling: in-memory rate-limit fragmentation across pods; Redis backing
needed for multi-pod deployment. Track when production scale-out begins.

### Ruling shape

```markdown
Decision 6 — Rate-limit substrate: **OPTION A APPROVED**.
AspNetCore built-in RateLimiter; in-memory backing for MVP; Redis backing as
forward-watch at multi-pod scale-out.
```

---

## Decision 7 — Invitation aggregate cluster

**V8 #3 ref:** §7.7 + §3.2
**Type:** Architecture / .NET-architect ratification

### Options

**Option A — New `packages/blocks-onboarding/` package**
- Greenfield cluster; clean separation
- Houses Invitation aggregate + accept-flow handlers + invitation email pipeline

**Option B — Fold into `packages/blocks-users/` (if emerging)**
- If a Users cluster emerges from cohort-2 onboarding sub-cohort 2 (Tenant + User
  aggregates), Invitation may fit there
- Tighter coupling; potentially clean if Users cluster is natural home

**Option C — Standalone `packages/blocks-invitations/`**
- Single-aggregate package; light
- Risk: too small to justify own package; admin invitations are part of broader
  user management

### Substrate implications

- **Option A**: New cluster; ~6 PRs per V8 #3 §5.4 sub-cohort 4 decomposition
- **Option B**: Cluster size grows; Users + Invitation aggregates share package
- **Option C**: Smallest; but might fragment further as related concerns emerge

### ONR recommendation

**Option A (`blocks-onboarding/`).** Reasoning:
- Onboarding is a coherent product capability (signup, verification, invitations,
  wizards, welcome flows); aggregating in one cluster matches the user-facing concept
- Future additions (e.g., team-onboarding-flows, 2FA-setup-flows) naturally land here
- Avoids prematurely committing to a Users cluster that may not emerge

### CIC input gap

None; .NET-architect ratification sufficient.

### Forward-watch

If Users cluster emerges and Invitation feels like a fit, reconsider folding.

### Ruling shape

```markdown
Decision 7 — Invitation aggregate cluster: **OPTION A APPROVED**.
New `packages/blocks-onboarding/` cluster houses Invitation + signup + welcome flows.
```

---

## Decision 8 — Tenant aggregate cluster

**V8 #3 ref:** §7.8 + §1.1
**Type:** Architecture / .NET-architect ratification

### Options

**Option A — New `packages/blocks-tenants/`**
- Greenfield cluster; clean separation
- Houses Tenant aggregate + ITenantContext concrete impl + tenant lifecycle

**Option B — Fold into `packages/blocks-onboarding/` (per Decision 7)**
- Tenant creation is part of onboarding flow; aggregate fits there
- Tighter coupling; both clusters touched at signup

**Option C — Fold into `packages/foundation-multitenancy/`**
- Tenant aggregate is conceptually foundational
- Risk: foundation packages are typically interface-only; aggregates muddy this

### Substrate implications

- **Option A**: Tenant cluster owns its lifecycle separately
- **Option B**: Cohesion with onboarding flow; less ceremony

### ONR recommendation

**Option B (fold into `blocks-onboarding/`).** Reasoning:
- Tenant aggregate is ONLY ever created during signup (per V8 #3 §1.1)
- Subsequent Tenant operations (rename, suspend, delete) are admin operations
  that probably belong in a future Admin cluster, not standalone Tenants cluster
- `blocks-onboarding/` cluster naturally houses both Tenant + Invitation
  aggregates (both are "onboarding-related state")
- Avoids cluster proliferation

### CIC input gap

None; .NET-architect ratification.

### Forward-watch

If Tenant admin operations grow substantial (rename, suspend, billing, etc.),
extract to dedicated cluster later.

### Ruling shape

```markdown
Decision 8 — Tenant aggregate cluster: **OPTION B APPROVED**.
Fold Tenant aggregate into `packages/blocks-onboarding/` cluster (per Decision 7).
Extract to dedicated cluster post-MVP if admin operations grow.
```

---

## Decision 9 — Cohort numbering allocation (W#NN through W#MM)

**V8 #3 ref:** §7.9
**Type:** Workstream tracking / Admiral allocation

### Options

**Option A — Allocate W#79–W#83 to 5 sub-cohorts**
- Sub-cohort 1 (substrate) = W#79
- Sub-cohort 2 (signup A+B) = W#80
- Sub-cohort 3 (wizard C) = W#81
- Sub-cohort 4 (invitations D) = W#82
- Sub-cohort 5 (polish) = W#83

**Option B — Single W#79 with sub-numbering**
- W#79.1 through W#79.5

**Option C — Defer until each sub-cohort enters Stage-05**
- No upfront allocation; Admiral allocates per-cohort
- Risk: confused workstream tracking during overlap

### ONR recommendation

**Option A.** Standard pattern (cohort = workstream); avoids special-case
sub-numbering; consistent with cohort-1 through cohort-4 precedent.

### CIC input gap

None; Admiral allocation authority.

### Forward-watch

Post-MVP: post-onboarding cohorts (e.g., 2FA, billing, federation) get
W#84+.

### Ruling shape

```markdown
Decision 9 — Cohort numbering: **OPTION A APPROVED**.
W#79 (substrate) → W#80 (signup A+B) → W#81 (wizard C) → W#82 (invitations D)
→ W#83 (polish).
```

---

## Decision 10 — Stage-05 hand-off authoring sequence

**V8 #3 ref:** §7.10
**Type:** ONR-internal sequencing

### Options

**Option A — Sub-cohort 1 (substrate) Stage-05 first; gate downstream on substrate ratification**
- Engineers can't build sub-cohort 2 until 1 substrate ships
- Author 2-5 Stage-05 in parallel post-substrate ratification

**Option B — All 4 hand-offs in parallel (1, 2, 3, 4)**
- Faster; but downstream sub-cohorts assume substrate decisions not yet ratified
- Risk: 2-4 hand-off revisions if substrate ratification changes shape

**Option C — Sub-cohort 1 then 2, then 3+4 in parallel, then 5**
- Sub-cohort 5 (polish) needs all prior; serial dependency
- Sub-cohort 3 (wizard) + 4 (invitations) can parallel post-2

### ONR recommendation

**Option C.** Sub-cohort 1 substrate must ratify before downstream hand-offs;
2 must precede 3+4 (User aggregate exists); 5 polish needs all prior.

### CIC input gap

None; ONR-internal sequencing.

### Forward-watch

If sub-cohort 2 surfaces unexpected substrate gaps, may need 2nd sub-cohort 1
amendment cycle.

### Ruling shape

```markdown
Decision 10 — Stage-05 sequencing: **OPTION C APPROVED**.
Sequence: 1 → 2 → (3 + 4 parallel) → 5. ONR authors per this order; sub-cohort
1 + 2 hand-offs ship sequentially before 3+4 parallel.
```

---

## Consolidated ruling template (Admiral consumes)

```markdown
---
type: ruling
from: admiral
to: onr (+ CIC for items 4-5)
re: V8 #3 onboarding-ladder 10 decisions
priority: medium
---

# Onboarding-ladder 10 decisions resolution

## Admiral rulings

Decision 1 — ADR 0095 Bootstrap Context: **OPTION A APPROVED**. ONR scaffold; Admiral authors.
Decision 2 — ADR 0096 Email Substrate: **OPTION C ACCEPTED** contingent on Decision 4.
Decision 3 — Self-tenant initial-write: **OPTION B APPROVED**. pattern-self-tenant-initial-write candidate.
Decision 4 — Email provider: **CIC ROUTES** (ONR recommends Postmark; CIC final).
Decision 5 — CAPTCHA: **CIC ROUTES** (ONR recommends Cloudflare Turnstile; CIC final).
Decision 6 — Rate-limit: **OPTION A APPROVED**. AspNetCore built-in.
Decision 7 — Invitation cluster: **OPTION A APPROVED**. New `packages/blocks-onboarding/`.
Decision 8 — Tenant cluster: **OPTION B APPROVED**. Fold into `blocks-onboarding/`.
Decision 9 — Cohort numbering: **OPTION A APPROVED**. W#79-83.
Decision 10 — Stage-05 sequencing: **OPTION C APPROVED**. 1 → 2 → (3+4 parallel) → 5.

## CIC questions routed

Q (Decision 4): Email provider — Postmark vs SendGrid vs SES vs Mailgun?
Q (Decision 5): CAPTCHA — Cloudflare Turnstile vs hCaptcha vs reCAPTCHA?

Pending CIC ratification before sub-cohort 1 ADR 0096 Stage-05 fires.

## Next ONR actions

1. Begin ADR 0095 scaffold (per Decision 1 Option A) — ~2-3h ONR work
2. Hold ADR 0096 scaffold (per Decision 2 Option C) pending Decision 4 resolution
3. Register pattern-self-tenant-initial-write candidate (per Decision 3)
4. Begin sub-cohort 1 Stage-05 hand-off draft (per Decision 10 Option C sequencing)
5. Allocate W#79 through W#83 in MASTER-PLAN.md (per Decision 9 Option A)
```

---

## Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T17-15Z` item V11 #4
2. V8 #3 onboarding-ladder Stage-02 scoping (shipyard#117) — source decisions list
3. V9 #4 onboarding-ladder sub-cohorts scaffold shells (shipyard#120) — sequencing precedent
4. V7 #3 MVP demo critical-path analysis (shipyard#111) — onboarding-ladder gap finding
5. V8 #4 ADR 0093 Rev 2 scaffolding for Admiral (shipyard#118) — ADR-scaffolding precedent
6. V11 #1 pattern-012 canonical framing (shipyard#124) — sub-pattern precedent for Decision 3
7. ADR 0091 R2 (ITenantContext) + ADR 0092 R2 (substrate tenant-keyed)
8. fleet-conventions §pre-auth requirements

---

## What ONR does next

V11 #4 deliverable complete. V11 batch state:
- V11 #1 HIGH done (shipyard#124; pattern-012 framing)
- V11 #2 done (shipyard#127; ADR 0094 consultation)
- V11 #3 done (shipyard#125; Maintenance migration scope)
- V11 #4 done (this doc)
- V11 forward-watch: V9 #2 + V9 #3 still conditional

ONR files V11 complete idle beacon. 4 of 4 V11 active items shipped. Awaits V12 dispatch.

— ONR, 2026-05-22T18:10Z
