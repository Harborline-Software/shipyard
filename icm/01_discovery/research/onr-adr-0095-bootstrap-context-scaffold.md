# ONR research — ADR 0095 Bootstrap Context scaffold (onboarding-ladder D1)

**Authored by:** ONR
**Requester:** Admiral (per `admiral-ruling-2026-05-25T1450Z-onboarding-ladder-10-decisions.md` Decision 1 — Option A APPROVED)
**Authored at:** 2026-05-25
**Type:** Research scaffold for new ADR 0095 (Onboarding Bootstrap Context)
**Status:** Draft for Admiral consumption — Admiral authors ADR 0095 Rev 1 text from this scaffold

---

## Scope of investigation

- **In scope:** Define the pre-tenant signup window, audit the status-quo gap, enumerate
  design options for a Bootstrap Context primitive, recommend an option with reasoning,
  surface halt conditions Admiral must resolve before authoring ADR 0095 Rev 1.
- **Out of scope:** Writing the ADR itself (Admiral territory per Decision 1); per-PR
  Stage-05 hand-off authoring (W79; downstream); email substrate / CAPTCHA / rate-limit
  designs (separate decisions 2, 4, 5, 6); ADR 0091 amendment authoring (the entire
  point of ADR 0095 is that the bootstrap window is OUT of ADR 0091's scope).
- **Authoritative sources consulted:** ADR 0091 Rev 2 (text + 16-symbol §A0 audit); ADR
  0031 (Wave 5.1 control-plane vs data-plane boundary); signal-bridge `Program.cs` SaaS
  composition root; signal-bridge `TenantSubdomainResolutionMiddleware`; signal-bridge
  `TenantRegistry.CreateAsync` (existing-but-test-only); `DemoTenantContext`;
  `IBrowserTenantContext`; foundation-authorization `AddSunfishTenantContext<TConcrete>`
  + `TenantContextScopeAssertion`; V8 #3 onboarding-ladder Stage-02 scoping; V11 #4
  10-decisions scaffold. Prior-art landmarks: Finbuckle.MultiTenant (multi-tenant SaaS
  framework canonical); ASP.NET Core `IHostBuilder` + `WebApplicationBuilder` pre-
  resolution patterns; Aspire app-host AppHost composition model.
- **Success criteria:** Admiral can author ADR 0095 Rev 1 from this scaffold without
  re-discovering the status-quo audit, the options space, or the dependency interactions.
  All decisions inside the ADR's scope have a recommended position; everything outside
  the ADR's scope has a halt condition naming who must resolve it.

---

## TL;DR

- **Problem.** Sunfish has no concept of a "pre-tenant request." Every request pipeline
  in signal-bridge today binds `IBrowserTenantContext` (data plane, via subdomain
  resolution) AND/OR `Sunfish.Foundation.Authorization.ITenantContext` (control plane,
  via the ADR 0091 facade). Both are post-tenant — the tenant must exist and be
  `Active` before either resolves. The public signup endpoint family (`POST /api/signup`,
  `GET /api/signup/verify-email/{token}`, `POST /api/invitations/accept/{token}`) has no
  binding to use. The middleware actively 404s apex-host requests (empty slug; reserved).
- **What ADR 0095 must define.** A small, **explicitly-pre-tenant** Bootstrap Context
  primitive that (a) declares the pipeline is in the pre-tenant window, (b) carries the
  bootstrap-relevant correlation surface (IP, antiforgery, CAPTCHA-token, rate-limit
  bucket, idempotency-key), and (c) is mutually exclusive in DI with the post-tenant
  bindings — so an analyzer (or `TenantContextScopeAssertion`-style runtime check) can
  fail-closed if a single request scope binds both.
- **Recommended option.** **Option C — `IBootstrapContext` as a distinct interface
  + dedicated `AddSunfishBootstrapContext` DI helper + analyzer-enforced mutual
  exclusion with the post-tenant facade.** Aligns with the ADR 0091 Rev 2 mental model
  (one DI helper per coherent context surface; runtime assertion verifies invariants).
  Avoids overloading either the post-tenant facade or `IBrowserTenantContext` with a
  pre-tenant mode flag — those would re-create the conflated-interface smell ADR 0091
  just resolved.
- **Halt conditions for Admiral.** (1) Cluster home for the Bootstrap-Context interface
  (foundation-authorization vs new foundation-bootstrap vs signal-bridge-local); (2)
  whether the apex-host signup-form endpoint family bypasses `TenantSubdomainResolutionMiddleware`
  entirely (new endpoint-routing branch) or is allowed to traverse it under an "apex
  exemption" flag; (3) sec-eng-council pre-merge review timing (ONR recommends MANDATORY
  council review on the Step 1 / Step 2 PRs, given this is a new pre-auth surface area).

---

## 1. Problem statement

### 1.1 What is the Bootstrap Context?

The "Bootstrap Context" is the request scope between **the first byte of an HTTP request
hitting Sunfish's apex host** and **the moment a tenant has been resolved (or created and
resolved)**. For 100% of Sunfish requests today, that window is zero-length — every
request either (a) carries a tenant subdomain that resolves via
`TenantSubdomainResolutionMiddleware` (data-plane requests), or (b) is rejected with 404
before any application code sees it (apex / unknown / `Pending` / reserved-slug requests).

Public signup is the first surface area that requires a non-zero pre-tenant window:

| Surface (per V8 #3 §1) | Pre-tenant window length | What the window must carry |
|---|---|---|
| `POST /api/signup` | start → `TenantRegistry.CreateAsync` returns | client IP, antiforgery token, CAPTCHA verdict, rate-limit bucket key, idempotency key, request-correlation ID |
| `GET /api/signup/verify-email/{signed-token}` | start → token-payload verification + `User.EmailVerified` write | client IP, signed-token payload, rate-limit bucket (per token), request-correlation ID |
| `POST /api/signup/check-email-available` | full request | client IP, antiforgery token, CAPTCHA-or-rate-limit, request-correlation ID |
| `POST /api/invitations/accept/{signed-token}` | start → invitation verification + `User` creation | client IP, signed-token payload, antiforgery, rate-limit bucket (per token), request-correlation ID |

The Bootstrap Context is the typed scoped DI primitive these four surfaces inject in
place of `IBrowserTenantContext` / `Sunfish.Foundation.Authorization.ITenantContext` /
`Sunfish.Foundation.MultiTenancy.ITenantContext`. Nothing about it is novel — every
multi-tenant SaaS framework has an equivalent. Sunfish has just never needed one before
because every existing surface was post-tenant.

### 1.2 Why this needs an ADR (vs an inline implementation note)

Three reasons make this substrate-tier and worth Admiral-authority ratification:

1. **Mutual exclusion with the post-tenant facade is security-critical.** The textbook
   confused-deputy seam ADR 0091 Rev 2 amendment A1 closes (a single request scope
   resolving more than one identity surface) re-opens immediately if the Bootstrap
   Context is allowed to coexist with the post-tenant facade in the same scope. Without
   an analyzer or runtime assertion, a developer who copy-pastes a "signup handler that
   also reads `ITenantContext.TenantId`" produces a request that creates a tenant **and
   then makes a control-plane decision against the demo tenant** (in dev) or against
   whichever tenant the DI container last resolved (in production with a non-`DemoTenantContext`
   impl — exact behavior is non-obvious and provider-dependent). This is the same shape
   of bug ADR 0091 R2 amendment A1 named as the highest-value invariant in the system.
   It deserves ADR-tier treatment.

2. **The interface shape constrains 4+ Stage-05 hand-offs downstream.** W79 (sub-cohort
   1 substrate), W80 (Surfaces A+B signup + verification), W82 (Surface D invitations),
   and ultimately the post-MVP federation / webhook-receiver surfaces all consume the
   Bootstrap Context. Choosing the wrong shape — e.g., reusing `IBrowserTenantContext`
   with a "null tenant" mode — propagates across the entire ladder; ADR captures it
   once and the hand-offs reference it.

3. **The cluster-home decision affects three repos.** The interface can live in
   shipyard `foundation-authorization` (alongside the post-tenant facade), in a new
   shipyard `foundation-bootstrap` package, or in signal-bridge-local. Each choice
   has different blast-radius implications (Sunfish desktop / Anchor consume some of
   these; tender / flight-deck may eventually). Cluster choice is exactly the call ADR
   0091 had to make at facade introduction — ADR-tier.

### 1.3 What ADR 0095 must NOT define

To stay scoped, ADR 0095 must explicitly **not** define:

- The signup endpoint's request/response payload shape (Stage-05 W80 hand-off).
- The CAPTCHA verdict-token format (Decision 5 + CIC ruling; downstream of provider).
- The rate-limit policy values (Decision 6 build; downstream of substrate ratification).
- The Tenant aggregate's persistence shape (W79 hand-off; consumes pattern-009 +
  ADR 0092 + the `pattern-self-tenant-initial-write` candidate per Decision 3).
- The audit-event types emitted from signup (ADR 0049 enum extension PR; downstream).

These all consume the Bootstrap Context; none of them belong in the substrate ADR.

---

## 2. Status-quo audit

### 2.1 Bootstrap-adjacent code that exists today

Findings from a targeted scan of signal-bridge + shipyard kernel:

| Symbol / path | Existing? | Role in pre-tenant window | Reusable as-is? |
|---|---|---|---|
| `Sunfish.Bridge.Middleware.TenantSubdomainResolutionMiddleware` | yes | Resolves subdomain → tenant; **404s on apex / reserved / `Pending`** | No — actively rejects the pre-tenant case |
| `Sunfish.Bridge.Middleware.IBrowserTenantContext` | yes | Data-plane scoped binding populated by ↑ middleware | No — throws on `IsResolved=false` reads (by design) |
| `Sunfish.Bridge.Middleware.BrowserTenantContext` | yes | Concrete impl of ↑ | No |
| `Sunfish.Foundation.Authorization.ITenantContext` (facade) | yes (ADR 0091 R2) | Sum-interface; tenant id + caller + authz | No — every member is post-tenant by definition |
| `Sunfish.Foundation.MultiTenancy.ITenantContext` | yes | Tenant-only identity surface (`Tenant: TenantMetadata?`, `IsResolved`) | **Closest analog** — but `IsResolved=false` reads are an exception path, not a designed state for the entire request |
| `Sunfish.Foundation.Authorization.ICurrentUser` | yes (ADR 0091 R2) | Caller identity surface | No — pre-tenant has no current user (anonymous) |
| `Sunfish.Foundation.Authorization.IAuthorizationContext` | yes (ADR 0091 R2) | Authz policy evaluation | No — pre-tenant has no policy to evaluate |
| `Sunfish.Foundation.Authorization.DependencyInjection.AddSunfishTenantContext<TConcrete>` | yes (ADR 0091 R2 A1) | DI helper; aliases 4 interfaces to one scoped instance | **Pattern to mirror** for the Bootstrap helper |
| `Sunfish.Foundation.Authorization.DependencyInjection.TenantContextScopeAssertion` | yes (ADR 0091 R2 A1) | `IHostedService` startup assertion: 4 interfaces resolve to same scoped instance | **Pattern to mirror** for cross-binding invariants |
| `Sunfish.Bridge.Services.ITenantRegistry.CreateAsync` | yes — but called only from tests | Inserts `TenantRegistration` with `Status=Pending` | **Yes — already exists**; signup endpoint wires it into a production code path for the first time |
| `Sunfish.Bridge.Authorization.DemoTenantContext` | yes (dev only) | Hardcoded tenant identity | Irrelevant to bootstrap; called out only to confirm dev/prod parity |
| Signal-bridge `Program.cs` SaaS-posture composition root | yes | Registers all post-tenant bindings + `TenantSubdomainResolutionMiddleware` | **Needs a parallel "bootstrap pipeline branch"** — see §4 |
| `signal-bridge/Sunfish.Bridge.Data/SunfishBridgeDbContext` constructor | yes | Takes scoped `Sunfish.Foundation.Authorization.ITenantContext` and captures `TenantId` for `HasQueryFilter` | **Risk** — signup must NOT touch this DbContext until the new tenant is resolved; otherwise the filter captures empty-string TenantId and lets the request see all-or-nothing legacy rows |

### 2.2 The four hard gaps

1. **No interface exists for "the request is in the pre-tenant window."** All three
   `ITenantContext` variants assume post-tenant.

2. **`TenantSubdomainResolutionMiddleware` actively rejects pre-tenant requests.** Apex
   host → empty slug → 404. The middleware would need either a per-route opt-out
   ("bootstrap routes bypass") or an "apex exemption" pathway. Either choice has API-
   surface implications — Admiral decision.

3. **`SunfishBridgeDbContext` captures `tenant.TenantId` in its constructor** for the
   `HasQueryFilter` lambda (lines 22-31). In the pre-tenant window, the captured
   value would be the empty string (per the ADR 0091 R2 facade default
   implementation: `Tenant?.Id.ToString() ?? string.Empty`). Legacy entities filter on
   `TenantId == _currentTenantId`, so an empty-string filter matches only rows that
   were inserted with empty TenantId — likely zero rows in production, but **the
   behavior is undefined** and depends on whether dev-seed data leaks empty strings.
   Signup MUST NOT touch this DbContext until a tenant exists. Resolved in Option C
   via "Bootstrap pipeline routes never resolve `SunfishBridgeDbContext`" — see §4.

4. **No existing code path creates a tenant in production.** `ITenantRegistry.CreateAsync`
   is called only from tests today (verified via repo-wide grep). Signup wires it into
   a production code path for the first time; that wiring belongs in the W79 Stage-05
   hand-off, **gated on ADR 0095 ratification**.

### 2.3 What about ADR 0091 Step 7+ ("facade deletion")?

ADR 0091 Step 7 marks the facade `[Obsolete]` and Step 7+ deletes it. The Bootstrap
Context **does not interact** with that scoping work — Bootstrap operates BEFORE any
tenant facade resolves, and post-facade-deletion the narrowed
`Sunfish.Foundation.MultiTenancy.ITenantContext` is still post-tenant. ADR 0091's Step
7+ forward-watch (`admiral-adr-0091-step-3-and-4-pre-research.md`) and ADR 0095's pre-
tenant scope do not overlap.

---

## 3. Options analysis

Five candidate designs for the Bootstrap Context primitive, ordered roughly by increasing
invasiveness:

### Option A — Inline (no interface; per-endpoint locals)

Surface A handler reads `HttpContext.Connection.RemoteIpAddress`, antiforgery, CAPTCHA-
verdict, idempotency-key, etc. directly from `HttpContext`. No new interface, no DI
binding, no analyzer.

- **Pro:** zero ceremony; smallest diff.
- **Pro:** no DI invariant to maintain.
- **Con:** every signup-family endpoint reimplements the same correlation/IP/captcha
  read; testing surface is `HttpContext` not a typed mockable object.
- **Con:** no analyzer can enforce "this endpoint is bootstrap-only" — a careless
  developer can read `ITenantContext.TenantId` inside the same handler and ship the
  confused-deputy seam.
- **Verdict:** Rejected — re-introduces the smell ADR 0091 R2 just closed. Substrate-
  tier change deserves substrate-tier ergonomics.

### Option B — Reuse `Sunfish.Foundation.MultiTenancy.ITenantContext` with `IsResolved=false`

Bind a Bootstrap implementation of the existing tenant context interface that always
returns `Tenant=null` / `IsResolved=false`. The pre-tenant pipeline already has the
binding shape; the "unresolved" branch is the bootstrap window.

- **Pro:** zero new interfaces.
- **Pro:** existing consumers that check `IsResolved` get a defined story for "not
  yet."
- **Con:** ADR 0091 R2's invariant assumes `AddSunfishTenantContext<TConcrete>` is
  called once and aliases four interfaces to a single scoped instance. A Bootstrap
  variant breaks this — either by skipping the helper (and tripping
  `TenantContextScopeAssertion`) or by aliasing four interfaces to a "null tenant"
  instance (and breaking the assumption that `Tenant` is non-null in any post-tenant
  consumer reading `Tenant.Id`).
- **Con:** Conflates "tenant exists but not yet resolved by middleware" (transient,
  middleware-mis-ordering bug — fail loudly) with "tenant intentionally absent because
  the request is pre-tenant" (intended state — happy path). The interface should
  distinguish these, not fold them.
- **Con:** Does not carry the bootstrap-specific surface area (CAPTCHA verdict, IP,
  idempotency key) — those still have to come from `HttpContext` ad-hoc.
- **Verdict:** Rejected — overloads the post-tenant interface to mean two different
  things, which is the conflated-interface smell pattern ADR 0091 just removed.

### Option C — `IBootstrapContext` as a distinct interface + dedicated `AddSunfishBootstrapContext` DI helper + mutual-exclusion enforcement [RECOMMENDED]

A new interface, `Sunfish.Foundation.Authorization.IBootstrapContext` (or in a new
`Sunfish.Foundation.Bootstrap` namespace — see halt condition 1), with:

- **Members:** `string CorrelationId`, `IPAddress? ClientIp`, `string? CaptchaToken`,
  `string? IdempotencyKey`, `string? RateLimitBucketKey`. All read-only; populated by
  a new `BootstrapContextResolutionMiddleware` running BEFORE the tenant-subdomain
  middleware (or on a parallel pipeline branch — see §4).
- **DI helper:** `AddSunfishBootstrapContext<TConcrete>()` mirroring the ADR 0091
  R2 helper. Aliases the interface to a single scoped instance.
- **Mutual exclusion:** A new `BootstrapContextMutualExclusionAnalyzer` (Roslyn) +
  runtime assertion. The analyzer fails closed when a single endpoint binds both
  `IBootstrapContext` and any post-tenant facade-or-narrowed interface
  (`Sunfish.Foundation.Authorization.ITenantContext`, `Sunfish.Foundation.MultiTenancy.ITenantContext`,
  `ICurrentUser`, `IAuthorizationContext`, `IBrowserTenantContext`). The runtime
  assertion `IHostedService` verifies at startup that no scoped registration of
  `IBootstrapContext` and `ITenantContext` resolves to the same backing instance.

- **Pro:** Distinguishes "pre-tenant by design" from "tenant unresolved due to bug."
- **Pro:** Mirrors the ADR 0091 R2 mental model and reuses its DI-helper +
  startup-assertion patterns — low cognitive cost for the team.
- **Pro:** Carries the bootstrap-specific surface area as first-class typed members.
- **Pro:** Analyzer + runtime assertion close the confused-deputy seam BEFORE the
  first request can hit it (matches ADR 0091 R2 A1's fail-closed posture).
- **Con:** New interface + DI helper + analyzer + assertion = ~5-8 files. Heavier
  than Option A or B.
- **Con:** Requires Admiral to decide cluster home (halt condition 1).
- **Verdict:** **Recommended.** Substrate-tier change merits substrate-tier ergonomics.
  The blast radius — 4 onboarding sub-cohorts plus post-MVP webhook + federation
  surfaces — justifies the up-front interface cost.

### Option D — Repurpose `IBrowserTenantContext` with a "pre-tenant" mode

Extend `IBrowserTenantContext` with an `IsBootstrap: bool` field; populate the
context as "bootstrap" on signup routes (bypassing the registry lookup).

- **Pro:** No new interface to manage.
- **Con:** `IBrowserTenantContext` is data-plane-specific (Ed25519 + Argon2id + slug
  + `TeamPublicKey` + `AuthSalt`); none of these surface in the pre-tenant signup
  window. Folding bootstrap into it bloats the interface and re-creates the
  multi-purpose-interface smell.
- **Con:** The IsResolved-vs-IsBootstrap state space becomes confusing — what does it
  mean to be `IsBootstrap && IsResolved`? Or `!IsBootstrap && !IsResolved`?
- **Verdict:** Rejected — same conflation pathology as Option B, applied to
  `IBrowserTenantContext` instead.

### Option E — Hybrid: nullable pre-tenant fields on an HttpContext extension

Add an `IBootstrapContext` extension method on `HttpContext` that lazily populates
from the request — no DI binding. The signup handler calls `HttpContext.GetBootstrapContext()`.

- **Pro:** No DI plumbing; smallest interface footprint.
- **Pro:** Easy to unit-test if `HttpContext` is mockable.
- **Con:** Loses analyzer enforcement (no DI binding to detect).
- **Con:** Reads-from-HttpContext directly is the Option A failure mode in disguise.
- **Con:** Breaks the ADR 0091 R2 ergonomics convention (scoped DI binding for every
  request-scope surface).
- **Verdict:** Rejected — saves a DI registration at the cost of the mutual-exclusion
  guarantee.

### Decision matrix

| Criterion | A (inline) | B (reuse MT) | C (new I/F) | D (extend Browser) | E (HttpContext ext) |
|---|---|---|---|---|---|
| Distinguishes pre-tenant from mid-tenant-unresolved | no | no | **yes** | partly | no |
| Carries bootstrap surface (CAPTCHA, IP, idempotency) | no | no | **yes** | no | partly |
| Analyzer can enforce mutual exclusion | no | no | **yes** | no | no |
| Mirrors ADR 0091 R2 ergonomics | no | partly | **yes** | no | no |
| Cluster-home decision needed | no | no | **yes** | no | no |
| Effort (files touched) | ~2 | ~3 | ~5-8 | ~3 | ~3 |
| Risk of confused-deputy regression | HIGH | HIGH | LOW | HIGH | HIGH |

Option C wins on every correctness criterion; pays for it with ~3-5 additional files of
ceremony.

---

## 4. Cross-fleet integration concerns

### 4.1 Interaction with `TenantSubdomainResolutionMiddleware`

The signup endpoint family lives on the apex host, not a tenant subdomain. Today the
apex host → empty slug → reserved → 404. Two pathways forward:

**Pathway 1 — Bootstrap pipeline branches BEFORE tenant-subdomain middleware.**

```
Request → UseRouting → branch on path:
                       ├─ /api/signup, /api/signup/*, /api/invitations/accept/* →
                       │   UseMiddleware<BootstrapContextResolutionMiddleware> →
                       │   UseRateLimiter → UseCaptcha → endpoint handler
                       └─ all other routes →
                           UseMiddleware<TenantSubdomainResolutionMiddleware> →
                           UseAntiforgery → UseAuthorization → endpoint handler
```

Implementation: `app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api/signup") ||
... , bootstrapBranch => { ... })`. Surfaces a clean "bootstrap pipeline branch" that
never touches the tenant middleware.

**Pathway 2 — Tenant-subdomain middleware learns an apex-exemption pathway.**

`TenantSubdomainResolutionMiddleware.InvokeAsync` gets a configurable allowlist of
apex paths (`/api/signup`, etc.) that, when matched, **bind a Bootstrap context
instead of 404ing** and call `_next(ctx)`.

ONR recommends **Pathway 1 (branch BEFORE)**. Rationale: the tenant-subdomain
middleware's invariant ("any request reaching application code has a resolved
`IBrowserTenantContext`") is itself a security gate per `IBrowserTenantContext`
xmldoc lines 18-19. Adding a "but if the path matches X, skip the gate" exception
weakens the middleware's documented contract. A separate pipeline branch is the
more honest factoring. Halt condition 2 names this for Admiral.

### 4.2 Interaction with ADR 0091 (ITenantContext divergence resolution)

ADR 0091 Rev 2 amendment A1 introduced `TenantContextScopeAssertion` to verify the
4-interface single-instance invariant. ADR 0095 adds a 5th interface
(`IBootstrapContext`) and a 5th invariant: **no scope simultaneously has an
`IBootstrapContext` binding AND any of the 4 post-tenant bindings**.

Recommended approach: a new `BootstrapAndTenantMutualExclusionAssertion` `IHostedService`
that introspects the registered `IServiceCollection` (NOT just the resolved instances —
see §4.2.1) and verifies the mutual exclusion at startup. Pair with a Roslyn analyzer
that flags handlers binding both via constructor injection.

### 4.2.1 Sub-finding — service-collection introspection is required

The ADR 0091 R2 `TenantContextScopeAssertion` resolves all 4 interfaces in a single
scope and verifies `ReferenceEquals`. For ADR 0095, we cannot mirror this exactly:
mutual exclusion means we want to verify that **resolving `IBootstrapContext` in a
scope that also resolves any post-tenant interface THROWS or is otherwise prevented**.
Two implementation paths:

- **Path α — runtime scope check.** At startup, attempt to resolve both
  `IBootstrapContext` and `ITenantContext` from a single scope; expect the resolution
  to throw or one to be unregistered.
- **Path β — service-collection introspection.** At startup, scan the
  `IServiceCollection` snapshot for both registrations; require that they live in
  **disjoint composition roots** (e.g., the Bootstrap pipeline branch's DI scope
  excludes the post-tenant facade). This is the cleaner answer but requires .NET 11+
  scoped DI containers to be branch-able.

ONR recommends **Path β** if .NET 11's `IServiceScope` supports nested-scope filtering;
falls back to Path α otherwise. This is an open implementation-detail question for
.NET-architect council to ratify when ADR 0095 reaches Stage-03 / Stage-05.

### 4.3 Interaction with ADR 0091 Step 3 — narrow-not-facade preference

Recent fleet directive (memory: `feedback_itenantcontext_consumption_qualification`)
codifies that consumption sites pick the Authorization sum-interface facade over the
MultiTenancy narrowed variant UNTIL ADR 0091 Step 3 narrows. For ADR 0095, this means:

- Bootstrap handlers should NOT need `Sunfish.Foundation.Authorization.ITenantContext`
  (the facade) — they're pre-tenant.
- The `IBootstrapContext` interface lives in a namespace that does not collide with
  either Authorization OR MultiTenancy. Recommendation: `Sunfish.Foundation.Bootstrap`
  (new namespace) — clean separation, no impact on the ongoing ADR 0091 Step 3 narrowing.

### 4.4 Interaction with pattern-009 (Bridge endpoint + frontend rebind pair)

The signup endpoint family is a NEW route family (4 endpoints). Per fleet conventions:
- pattern-009 SPOT-CHECK MANDATORY (security-engineering council) at PR-open.
- For ADR 0095 itself: pre-merge council review (security-engineering + .NET-architect)
  is mandatory per ADR 0069 (ADR Authoring Discipline) — this is substrate-tier and
  security-critical.

### 4.5 Interaction with multi-tenant isolation

`SunfishBridgeDbContext` captures `tenant.TenantId` in its constructor (line 22-31).
In the Bootstrap window the captured value would be empty-string. Signup MUST NOT
touch `SunfishBridgeDbContext` until after `ITenantRegistry.CreateAsync` returns AND
the request scope rebinds to the new tenant.

This produces a subtle ordering constraint inside the signup handler:

```
1. BootstrapContext is the only binding active in DI scope (verified by analyzer).
2. Validate signup payload (no DB access).
3. Check email availability via a dedicated readonly DbContext (control-plane only;
   does NOT consume ITenantContext).  [implementation-detail; W79 hand-off scope]
4. Call ITenantRegistry.CreateAsync (this writes to control-plane TenantRegistrations,
   which is OUTSIDE the tenant-query-filter set in SunfishBridgeDbContext — see
   §2.1; TenantRegistrations is a control-plane DbSet per ADR 0031).
5. Create the initial User aggregate (data plane; needs the new tenant context).
   THIS is where ADR 0095 hands off to the post-tenant world.
6. Sign welcome email link, emit audit events, dispatch email.
```

Step 5 is the boundary. ADR 0095 must define **how Step 5 transitions from
`IBootstrapContext` scope to post-tenant scope**. Two candidate mechanisms:

- **Mechanism α — scope termination + child scope.** The signup handler explicitly
  creates a child `IServiceScope` with a `DemoTenantContext`-equivalent backed by
  the just-created tenant; the child scope writes the User aggregate; the child
  scope disposes; the outer Bootstrap scope continues for audit + email.
- **Mechanism β — `TenantContextScopeAssertion` exempts signup handlers via
  attribute.** The signup endpoint is decorated with `[BootstrapEndpoint]`; the
  startup assertion sees the attribute and exempts the scope from the mutual-exclusion
  check.

ONR recommends **Mechanism α** — child scope is the .NET-idiomatic pattern, doesn't
require attribute-driven exemptions, and is testable. Mechanism β is more compact but
introduces an exemption pathway that future maintainers may copy badly. Halt condition
2-β (sub-finding) names this as an open Admiral question.

---

## 5. Reference architectures (prior art)

ONR surveyed three reference architectures that solve the same pre-tenant signup
problem. Findings:

### 5.1 Finbuckle.MultiTenant (canonical .NET multi-tenant SaaS framework)

Finbuckle's `ITenantInfo` has a similar shape to Sunfish's `Sunfish.Foundation.MultiTenancy.ITenantContext`
— identity-only. Finbuckle's solution to the pre-tenant problem is **route-based
opt-out via `[SkipTenantResolution]` attribute** + middleware that bypasses tenant
resolution on attributed endpoints. The signup endpoint is decorated; the request
runs without an `ITenantInfo` binding.

- **Lesson learned:** Finbuckle's choice is closest to ADR 0095 Option D — extend the
  tenant interface with an opt-out attribute. The Sunfish equivalent would be Option B
  + an attribute. ONR rejects it for the same reason ADR 0091 rejected the conflated
  interface: attribute-driven mutual exclusion is more error-prone than DI-driven
  mutual exclusion.
- **Tier:** Primary source — `Finbuckle/Finbuckle.MultiTenant` GitHub repo, README +
  middleware source.

### 5.2 ASP.NET Core IHostBuilder / WebApplicationBuilder pre-resolution patterns

ASP.NET Core itself has a pre-resolution pattern: `IHostEnvironment`, `IConfiguration`,
`ILogger<T>` are all available BEFORE any tenant resolution. They're host-scoped, not
request-scoped. The pre-tenant request window in Sunfish has a structurally similar
need: a small, request-scoped surface that's available **before** tenant resolution.

- **Lesson learned:** The host-scoped pre-resolution pattern doesn't translate
  directly to Sunfish (per-request, not per-app) — but the **shape** of "small,
  scoped, with explicit contract about what it's for" maps to ADR 0095 Option C.
- **Tier:** Primary source — Microsoft.Extensions.Hosting source + ASP.NET Core
  middleware pipeline docs.

### 5.3 Aspire app-host model (currently in preview)

Aspire's `AppHost.cs` composition model wires services in a parent-host scope; per-
tenant services are layered on top via service-registration extension methods. The
Sunfish bridge's `ConfigureSaasPosture` is patterned after this.

- **Lesson learned:** The bootstrap pipeline branch (§4.1 Pathway 1) can be expressed
  as a separate Aspire-style service-registration extension method —
  `AddSunfishBootstrapPipeline` — that the SaaS-posture composition root opts into.
  Symmetric with `AddSunfishTenantContext`.
- **Tier:** Primary source — Aspire preview docs, `dotnet/aspire` GitHub repo.

### 5.4 Synthesis

All three reference architectures converge on the same answer Option C codifies: **a
small, request-scoped, single-purpose surface, registered via a dedicated DI helper,
verified by a startup invariant**. This is not novel — Sunfish just hasn't had to
build it before because every prior surface was post-tenant.

---

## 6. Recommended option (consolidated)

**Option C: `IBootstrapContext` as a distinct interface + `AddSunfishBootstrapContext`
DI helper + analyzer + startup mutual-exclusion assertion.**

### 6.1 Interface sketch (for Admiral's ADR Rev 1 §Decision)

```csharp
namespace Sunfish.Foundation.Bootstrap;

/// <summary>
/// Scoped per-request surface for the pre-tenant window. Mutually exclusive with
/// Sunfish.Foundation.Authorization.ITenantContext (and its narrowed variants);
/// startup assertion + Roslyn analyzer enforce the exclusion.
/// </summary>
/// <remarks>
/// Bound by BootstrapContextResolutionMiddleware on the signup pipeline branch
/// (per ADR 0095 §4.1 Pathway 1).
/// </remarks>
public interface IBootstrapContext
{
    /// <summary>Stable request correlation ID; flows into logs + audit events.</summary>
    string CorrelationId { get; }

    /// <summary>Client IP (post-X-Forwarded-For evaluation); null when the
    /// underlying connection has no addressable peer (test contexts).</summary>
    System.Net.IPAddress? ClientIp { get; }

    /// <summary>CAPTCHA verdict token from the form payload; null pre-verification
    /// or for endpoints that don't require CAPTCHA (verify-email).</summary>
    string? CaptchaToken { get; }

    /// <summary>Idempotency key from the X-Idempotency-Key header; null when the
    /// caller didn't supply one (signup itself is non-idempotent; invitation
    /// accept is idempotency-required).</summary>
    string? IdempotencyKey { get; }

    /// <summary>Bucket key for the AspNetCore RateLimiter (per-IP + per-route).</summary>
    string RateLimitBucketKey { get; }
}
```

### 6.2 DI helper sketch

```csharp
namespace Sunfish.Foundation.Bootstrap.DependencyInjection;

public static class BootstrapContextServiceCollectionExtensions
{
    public static IServiceCollection AddSunfishBootstrapContext<TConcrete>(
        this IServiceCollection services)
        where TConcrete : class, IBootstrapContext
    {
        services.AddScoped<TConcrete>();
        services.AddScoped<IBootstrapContext>(sp => sp.GetRequiredService<TConcrete>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            IHostedService, BootstrapAndTenantMutualExclusionAssertion>());
        return services;
    }
}
```

### 6.3 Migration / shipping plan (rough — Admiral refines)

| Step | PR scope | Council |
|---|---|---|
| 1 | New `packages/foundation-bootstrap/` package; `IBootstrapContext` + DI helper + assertion; no production consumer | .NET-architect + security-engineering (substrate-tier; pre-auth surface) |
| 2 | `BootstrapContextResolutionMiddleware` + bridge SaaS-posture composition root opts in via `app.UseWhen(...)`; new branch is empty (no endpoints yet) | security-engineering MANDATORY (pipeline-routing change) |
| 3 | Roslyn analyzer `BootstrapAndTenantMutualExclusionAnalyzer` ships; CI gate added | .NET-architect |
| 4 | Stage-05 W79 hand-off references ADR 0095 § Decision and Steps 1-3 as substrate prerequisites; first consumer is `POST /api/signup` | (downstream — not ADR 0095's responsibility) |

### 6.4 Why Option C wins over the smaller alternatives

The temptation with substrate-tier ADRs is to pick the smallest workable option (B or
E) to ship faster. ADR 0091 R2's history is instructive: Rev 1 picked the smaller
option (no analyzer); council came back AMBER and required the analyzer (A2) +
runtime assertion (A1). Adopting the fully-hardened shape up-front avoids that
churn. Option C IS the fully-hardened shape for pre-tenant context.

Standing memo `feedback_prefer_cleanest_long_term_option` codifies this preference.

---

## 7. Halt conditions for Admiral

The following questions are outside ONR research scope and require Admiral (or
council-via-Admiral) ratification before ADR 0095 Rev 1 ships.

### Halt 1 — Cluster home for `IBootstrapContext`

Three candidate locations:

| Location | Pro | Con |
|---|---|---|
| `packages/foundation-authorization/` (alongside ADR 0091 R2 interfaces) | Co-located with the post-tenant interface; one mental model | Conflates a pre-tenant surface with the post-tenant cluster the team has just defined |
| **`packages/foundation-bootstrap/`** (new) | Clean separation; namespace makes pre-tenant nature explicit | One more package to maintain |
| `signal-bridge/Sunfish.Bridge/Bootstrap/` (bridge-local) | Smallest blast radius; matches "Sunfish has one host today" reality | If Sunfish desktop / Anchor / future hosts ever need a pre-tenant surface they each re-implement |

**ONR recommendation:** `packages/foundation-bootstrap/` (new package). Mirrors the ADR
0091 Step 1 corrigendum (a new `foundation-authorization` package was extracted for the
same reason — avoid mixing namespaces that should be conceptually separable).

### Halt 2 — Pipeline routing approach

Per §4.1: Pathway 1 (separate bootstrap pipeline branch via `app.UseWhen`) vs Pathway 2
(tenant-subdomain middleware learns an apex-exemption).

**ONR recommendation:** Pathway 1. Cleaner factoring; preserves `TenantSubdomainResolutionMiddleware`
xmldoc contract.

### Halt 2-β (sub-finding) — Bootstrap → post-tenant transition mechanism

Per §4.5: Mechanism α (child scope inside the handler) vs Mechanism β (attribute
exemption to the startup assertion).

**ONR recommendation:** Mechanism α. .NET-idiomatic; testable; doesn't introduce
exemption pathways.

### Halt 3 — Council review timing

ADR 0091 R2 was reviewed by both .NET-architect AND security-engineering councils.
ADR 0095 is also substrate-tier + security-critical (introduces a pre-auth surface
area; the analyzer + assertion close a confused-deputy seam).

**ONR recommendation:** MANDATORY pre-merge council review by BOTH councils on the
ADR text and on the Step 1 + Step 2 implementation PRs. The Step 3 analyzer-ship PR
needs .NET-architect council; sec-eng spot-check optional.

### Halt 4 — Per-IP vs per-tenant CAPTCHA + rate-limit policy

`IBootstrapContext.RateLimitBucketKey` is a single string. Should bootstrap routes use
per-IP buckets (anti-DDoS) or per-route+per-IP buckets (anti-brute-force)? Most
likely both layers — but the policy values (window size, request limits) are a CIC /
sec-eng decision downstream.

**ONR recommendation:** ADR 0095 defines the property and assigns the policy decision
to W79 Stage-05 hand-off (sec-eng SPOT-CHECK on the Step 2 PR confirms the values).
Not an ADR 0095 blocker.

### Halt 5 — `[BootstrapEndpoint]` marker attribute existence

For the analyzer to find bootstrap endpoints in the codebase, the endpoints need a
marker. Two candidates:

- A `[BootstrapEndpoint]` attribute on the endpoint method.
- A `MapBootstrapEndpoints(IEndpointRouteBuilder)` extension method that endpoints
  call into (no attribute; the registration site is the marker).

**ONR recommendation:** the extension-method approach (`MapBootstrapEndpoints`).
Attributes are easy to forget; extension methods make the bootstrap nature explicit
at registration time. Halt condition retained because it changes ADR 0095's surface
slightly.

### Halt 6 — Forward-watch: webhook-receiver + cross-tenant federation

The Admiral ruling's Forward-watch section names two future bootstrap-context
consumers: webhook-receiver bootstrap (e.g., Stripe webhook with no tenant in the
URL) and cross-tenant federation bootstrap (e.g., a federated query that arrives at
the apex host with a federation token). ADR 0095 should mention these forward-watch
items in §"Out of scope but flagged" — but Admiral may want to confirm the scope
boundary now: is webhook-receiver going to use `IBootstrapContext` directly, or a
separate `IWebhookContext` that extends `IBootstrapContext`?

**ONR recommendation:** mention as forward-watch; do not commit either way in
ADR 0095 Rev 1. If a 2nd-instance consumer (webhook-receiver) emerges that needs
identical surface, promote `IBootstrapContext` to the base interface and add
`IWebhookContext : IBootstrapContext`. Mirrors V11 #1's sub-pattern split precedent.

---

## 8. Open questions ONR could not resolve from the codebase

These would benefit from a follow-up research dispatch or council consultation but
do not block ADR 0095 scaffolding:

1. **.NET 11 nested-scope filtering** — does .NET 11 `IServiceScope` support
   nested-scope filtering well enough to implement §4.2.1 Path β cleanly? (Falls
   back to Path α — runtime resolution-attempt check — which is functional but
   uglier.) .NET-architect council can resolve.

2. **Existing antiforgery middleware ordering** — `Program.cs` line 172 calls
   `app.UseAntiforgery()` AFTER `UseMiddleware<TenantSubdomainResolutionMiddleware>`.
   If the Bootstrap pipeline branch needs antiforgery (signup form POSTs), does
   `UseWhen` correctly inherit antiforgery state? Implementation-detail; Engineer or
   Stage-05 hand-off resolves.

3. **`DemoAuthWarningFilter` interaction** — the dev-loop `DemoAuthWarningFilter`
   (lines 290-294 in Program.cs) emits a warning when `DemoTenantContext` is bound.
   In the Bootstrap pipeline branch, no `DemoTenantContext` is bound; does the
   filter mis-attribute this as "dev tenant not bound"? Implementation-detail;
   Engineer resolves.

4. **`SunfishBridgeDbContext` lifecycle in Bootstrap scope** — does the Bootstrap
   pipeline branch need to opt OUT of `AddDbContext<SunfishBridgeDbContext>`
   resolution to avoid the empty-string filter problem from §2.2? Or is a separate
   read-only DbContext warranted for the email-uniqueness check? Implementation-detail;
   W79 Stage-05 hand-off authoring resolves; ADR 0095 just states the invariant
   ("bootstrap scope MUST NOT resolve `SunfishBridgeDbContext`").

---

## 9. Sources cited

**Primary (publication + retrieval dates):**

1. ADR 0091 Rev 2 (ITenantContext divergence resolution) — `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md`; promoted-to-Accepted 2026-05-19; retrieved 2026-05-25.
2. ADR 0031 (Bridge as Hybrid Multi-Tenant SaaS) — `shipyard/docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md`; retrieved 2026-05-25.
3. signal-bridge `Sunfish.Bridge/Program.cs` SaaS-posture composition root — retrieved 2026-05-25.
4. signal-bridge `Sunfish.Bridge/Middleware/TenantSubdomainResolutionMiddleware.cs` — retrieved 2026-05-25.
5. signal-bridge `Sunfish.Bridge/Middleware/IBrowserTenantContext.cs` — retrieved 2026-05-25.
6. signal-bridge `Sunfish.Bridge/Services/TenantRegistry.cs` — retrieved 2026-05-25.
7. signal-bridge `Sunfish.Bridge/Authorization/DemoTenantContext.cs` — retrieved 2026-05-25.
8. shipyard `packages/foundation-authorization/ITenantContext.cs` (facade) — retrieved 2026-05-25.
9. shipyard `packages/foundation-authorization/DependencyInjection/TenantContextServiceCollectionExtensions.cs` — retrieved 2026-05-25.
10. shipyard `packages/foundation-authorization/DependencyInjection/TenantContextScopeAssertion.cs` — retrieved 2026-05-25.
11. shipyard `packages/foundation-multitenancy/ITenantContext.cs` — retrieved 2026-05-25.
12. signal-bridge `Sunfish.Bridge.Data/SunfishBridgeDbContext.cs` (constructor + filter section) — retrieved 2026-05-25.

**Secondary:**

13. V8 #3 onboarding-ladder Stage-02 scoping — `shipyard/icm/01_discovery/research/onboarding-ladder-stage-02-scoping.md` (branch `onr/v8-3-onboarding-stage-02`); authored 2026-05-22; retrieved 2026-05-25.
14. V11 #4 10-decisions resolution scaffold for Admiral ruling — `shipyard/icm/01_discovery/research/onboarding-ladder-10-decisions-scaffold-for-admiral-ruling.md` (branch `onr/v11-4-onboarding-10-decisions`); authored 2026-05-22; retrieved 2026-05-25.
15. Admiral ruling on 10 decisions — `coordination/inbox/admiral-ruling-2026-05-25T1450Z-onboarding-ladder-10-decisions.md`; authored 2026-05-25; retrieved 2026-05-25.
16. ADR 0094 (IAuditEventReader) — `shipyard/docs/adrs/0094-i-audit-event-reader.md` — for ADR-scaffolding cadence reference; retrieved 2026-05-25.
17. `feedback_itenantcontext_consumption_qualification` memory entry — codifies the facade-vs-narrowed preference until ADR 0091 Step 3 narrows.
18. `feedback_prefer_cleanest_long_term_option` memory entry — codifies the bias to substrate-correct over ship-fast.

**Tertiary (anecdotal / framework convention):**

19. Finbuckle.MultiTenant `[SkipTenantResolution]` attribute pattern — `Finbuckle/Finbuckle.MultiTenant` GitHub README; retrieved 2026-05-25.
20. ASP.NET Core `IHostBuilder` / `WebApplicationBuilder` pre-resolution pattern — Microsoft Learn docs general knowledge.
21. .NET Aspire app-host composition model — Aspire preview docs general knowledge.

---

## 10. What ONR does next

Per the Admiral ruling kickoff sequence:

1. This scaffold ships (PR open, status beacon filed).
2. Admiral consumes the scaffold; authors ADR 0095 Rev 1 (Admiral territory, NOT ONR).
3. ADR 0095 Rev 1 enters council review (sec-eng + .NET-architect per Halt 3 above).
4. Post-ratification, ONR begins:
   - Register `pattern-self-tenant-initial-write` candidate in the catalog (per
     Admiral ruling Decision 3).
   - Begin W79 (sub-cohort 1 substrate) Stage-05 hand-off authoring.
5. ADR 0096 scaffold remains on hold per Admiral ruling Decision 2; ONR files
   `onr-status-*` if 2026-06-08 approaches without CIC ruling on Decision 4.

— ONR, 2026-05-25
