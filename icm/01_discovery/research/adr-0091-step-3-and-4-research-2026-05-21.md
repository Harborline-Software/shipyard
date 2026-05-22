# ONR research — ADR 0091 Steps 3 + 4 pre-research (2026-05-21)

**Requester:** Admiral (per `admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #1)
**Authored by:** ONR
**Authored at:** 2026-05-21T11-58Z
**Status:** draft (ratification pending sec-eng + .NET-architect council review on the analyzer design + consumption-sweep scope)

---

## Scope of investigation

- **In scope:** ADR 0091 R2 Step 3 (test migration) + Step 4 (facade `[Obsolete]` + `RequestContextMixingAnalyzer` ship) implementation pre-research. The V2 directive frames these as "Step 3.0 consumption sweep across all repository implementations" + "Step 4.0 cross-cluster propagation verification" — a broader reading of the ADR's terse spec; this research covers BOTH interpretations.
- **Out of scope:** Step 5 facade deletion (post-one-cohort grace; future); ADR 0092 EFCore query-filter operator (V2 #2 separate research); production OIDC-impl ADR (V1 #5 scoping research already shipped at shipyard#59).
- **Authoritative sources consulted:** ADR 0091 R2 (Accepted; promoted 2026-05-19T02:40Z); `packages/foundation-authorization/` post-Step-1 source (verified 2026-05-21T11:55Z); ITenantContext consumer survey across `packages/` + `signal-bridge/`; cohort-1 PR 4 (ADR 0091 Step 1 — extract Authorization package + sum-interface facade, MERGED at shipyard#44); cohort-2 PR 0 cluster (a/b/c/d ALL MERGED — tenant-keyed repository contracts across financial cluster); `foundation-wayfinder-analyzers` Roslyn precedent.
- **Success looks like:** Engineer can open Step 3 + Step 4 PRs using this research doc's consumer inventory + analyzer design spec + test-migration sequence + propagation-rule documentation as the implementation scaffold; sec-eng + .NET-architect councils can SPOT-CHECK against the inventory.

---

## TL;DR

1. **Step 3 scope (test migration) is bounded** — `DemoTenantContext` + test fixtures across ~12 consumer packages follow production consumer shape after Step 2.x. Sequencing: lands AFTER Step 2.1+ batched endpoint migrations complete; ~1-2h per consumer × ~12 consumers = ~12-24h Engineer effort total OR ~6-8 PRs.

2. **Step 4 scope (analyzer + `[Obsolete]` mark) is the higher-risk slice** — `RequestContextMixingAnalyzer` Roslyn analyzer ships in the SAME PR as the `[Obsolete]` attribute on the facade. Precedent: `foundation-wayfinder-analyzers` pattern. Analyzer fails closed when both `IBrowserTenantContext` and any control-plane facade-or-narrowed interface are injected into the same endpoint.

3. **Directive's "Step 3.0 consumption sweep" extends the canonical scope** — beyond test-fixture migration, the directive frames Step 3.0 as a consumption sweep across ALL repository implementations. ONR's read: this is the implementation parallel to ADR 0091 R2's "Step 2.1+ batched endpoint migrations" but extended to repository-INTERNAL consumption (not just endpoint-handler-level consumption). Likely the Engineer's actual scope of work on the foundation-authorization migration AFTER Step 2.0 lands.

4. **Directive's "Step 4.0 propagation verification" is the analyzer + a runtime cross-cluster verification** — the analyzer enforces the no-mix invariant compile-time; runtime verification (e.g., a startup health-check that asserts no cross-cluster pipeline-mixing has been registered) provides defense-in-depth.

5. **Consumer inventory: 9 packages reference `Foundation.Authorization`** (facade); 6 packages reference `Foundation.MultiTenancy` (the narrowed surface). The lists overlap (financial-payments + financial-ledger reference both). Full inventory in §3.

6. **Sum-interface decomposition extension is NOT needed** — the 3 new interfaces (`ICurrentUser` + `IAuthorizationContext` + `Foundation.MultiTenancy.ITenantContext`) plus the facade are sufficient for current consumer needs. Future sum-interface members (e.g., `ISession` for refresh-token surface in production OIDC) are out-of-scope for Steps 3+4.

7. **Open question for .NET-architect council:** does the directive's "Step 3.0 consumption sweep" mean (a) extend cohort-2's tenant-keyed repository contract pattern to the remaining packages that lack it, OR (b) audit every consumer's ITenantContext usage for narrowing opportunities (consumer reads only `Tenant` → narrow to `Foundation.MultiTenancy.ITenantContext` only). ONR's read: (b); (a) is cohort-by-cohort substrate work, not Step 3 of ADR 0091. **Recommended Admiral routes a question to .NET-architect for confirmation.**

---

## 1. Current state — post-Step-1 + post-cohort-2-PR-0 cluster

### 1.1 `packages/foundation-authorization/` (Step 1 output)

Verified 2026-05-21T11:55Z:

| File | Purpose |
|---|---|
| `ITenantContext.cs` | The sum-interface facade per ADR 0091 R2 amendment 1. Extends `Foundation.MultiTenancy.ITenantContext, ICurrentUser, IAuthorizationContext`. Default-impl `string TenantId => Tenant?.Id.ToString() ?? string.Empty`. |
| `ICurrentUser.cs` | New (Step 1). `UserId: string` + `Roles: IReadOnlyList<string>`. |
| `IAuthorizationContext.cs` | New (Step 1). `bool HasPermission(string permission)`. |
| `DependencyInjection/TenantContextServiceCollectionExtensions.cs` | `AddSunfishTenantContext<TConcrete>` helper per amendment A1. Registers TConcrete as all four interfaces (same-instance resolution). |
| `DependencyInjection/TenantContextScopeAssertion.cs` | `IHostedService` startup assertion per amendment A1. Verifies all four interfaces resolve to the same scoped instance. |
| `tests/TenantContextFacadeTests.cs` | Step 1 acceptance tests; facade behavior + DI helper behavior + scope-assertion behavior. |
| `tests/Sunfish.Foundation.Authorization.Tests.csproj` | Test project. |
| `Sunfish.Foundation.Authorization.csproj` | Package project. |

**Step 1 status:** SHIPPED in shipyard#44 (merged 2026-05-19T10:39Z).

### 1.2 Step 2.0 status

Per V1 #3 research (shipyard#56) — Step 2.0 implementation pre-research is shipped; Engineer's Step 2.0 PR is the next downstream consumer of that research. Step 2.0 PR has NOT been opened yet at time of this V2 #1 research authoring (2026-05-21T11:58Z); Step 3 + 4 are downstream of Step 2.0 landing.

**Sequencing implication:** Step 3 + 4 research can ship NOW (this doc); Step 3 + 4 IMPLEMENTATION must wait until Step 2.0 + Step 2.1+ have landed.

### 1.3 Cohort-2 PR 0 cluster (orthogonal but informative)

All four PR 0 PRs MERGED in the gap (2026-05-20T23:40Z to 2026-05-21T00:16Z):

| PR | Subject |
|---|---|
| #52 PR 0a | `IInvoiceRepository` tenant-keyed (blocks-financial-ar) |
| #57 PR 0b | `IBillRepository` tenant-keyed (blocks-financial-ap) |
| #60 PR 0c | `IPaymentRepository` + `IPaymentApplicationRepository` tenant-keyed (blocks-financial-payments) |
| #64 PR 0d | `IJournalStore` tenant-keyed (blocks-financial-ledger) |

Plus #63 — pattern-009 corrigendum (dual SPOT-CHECK + pattern-009-tenant-keying-retrofit candidate ratified). These cohort-2 substrate PRs are CLUSTER work (financial-cluster repos); they do NOT change `ITenantContext` (cohort-2 PR 0 changes repository CONTRACTS, not the tenant-context interface).

**Cross-reference for Step 3.0 consumption sweep (directive framing):** the cohort-2 PR 0 cluster establishes the precedent for "tenant-keyed repository contract" across financial repos. The directive's "consumption sweep across all repository implementations" framing suggests extending this to the REMAINING repos: `blocks-leases` (`ILeaseService.ListAsync` per scope survey notes); `blocks-maintenance` (`IMaintenanceService` per cohort-1 forward-watch — Option D service-layer guards shipped at PR #38); `blocks-messaging` (per WS-E addendum forward-watch); `blocks-public-listings`; `blocks-businesscases`; `blocks-subscriptions`.

But these are NOT all "next step" candidates for ADR 0091 Step 3 — they're cohort-by-cohort substrate retrofits, scoped to their own workstreams. The directive's "Step 3.0 consumption sweep" is ambiguous on this point; see §6 open question 1.

---

## 2. ADR 0091 R2 canonical Step 3 + Step 4 spec

### 2.1 Canonical Step 3 spec (verbatim)

> **Step 3 — test migration.** Test fixtures (`DemoTenantContext` + test doubles) follow production consumers. Lands after Step 2.x is complete so fixtures track real shape.

Implementation checklist item:
> [ ] **Step 3 — test migration**
>   [ ] Test fixtures (`DemoTenantContext`, test doubles) follow production consumer shape

**Plain English:** after Step 2.0 (DbContext rewrite) + Step 2.1+ (endpoint migrations narrow to `Foundation.MultiTenancy.ITenantContext` / `ICurrentUser` / `IAuthorizationContext` as appropriate), the test fixtures (DemoTenantContext + test doubles in each consumer package's tests) need to track the new narrowed shape. This is mechanical: each test that constructs an ITenantContext mock updates the construction to use the right interface variant.

### 2.2 Canonical Step 4 spec (verbatim)

> **Step 4 — facade `[Obsolete]` + analyzer ship.** Mark `Foundation.Authorization.ITenantContext` `[Obsolete]`; ship `Sunfish.Foundation.Authorization.Analyzers.RequestContextMixingAnalyzer` (Roslyn) following the existing `foundation-wayfinder-analyzers` pattern (Revision 2 amendment 6 / A2 — Admiral-approved deferral from Step 1 to Step 4). Analyzer fails closed when both `IBrowserTenantContext` and any control-plane facade-or-narrowed interface are injected into the same endpoint.

Implementation checklist item:
> [ ] **Step 4 — facade `[Obsolete]` + analyzer**
>   [ ] Mark `Foundation.Authorization.ITenantContext` `[Obsolete]` (citing redirect targets)
>   [ ] Ship `Sunfish.Foundation.Authorization.Analyzers.RequestContextMixingAnalyzer` (amendment 6 / A2) following `foundation-wayfinder-analyzers` pattern; fails closed on `IBrowserTenantContext` + control-plane mix in same endpoint

**Plain English:** add `[Obsolete]` attribute to the facade interface in `foundation-authorization/ITenantContext.cs` (citing the three narrowed interfaces + `AddSunfishTenantContext<T>` helper as the redirect targets). Ship the Roslyn analyzer in the same PR; the analyzer scans endpoint registration sites + DI containers for cases where `IBrowserTenantContext` (data-plane subdomain context) is registered alongside the facade or any of the three narrowed control-plane interfaces. Fails the build on detection.

### 2.3 Directive's broader framing

Per `admiral-directive-2026-05-21T09-15Z` line 17-20:

> - Step 3.0 — ITenantContext consumption sweep across all repository implementations
> - Step 4.0 — Cross-cluster ITenantContext propagation verification

**Interpretation:**

The directive uses the `.0` suffix convention from Step 2.0 (Step 2.0 = the major DbContext rewrite PR; Step 2.1+ = subsequent endpoint-batched migrations). This suggests Step 3.0 + 4.0 are the FIRST PR in each step's potential sub-phase decomposition.

Reading the directive's bullet text:

- **Step 3.0 — "ITenantContext consumption sweep across all repository implementations":** broader than test-fixture migration. Implies a sweep across the production repository code (the consumers identified in §1.3) to identify which can narrow from facade to a single interface variant. This is what ADR 0091 R2 calls Step 2.1+ batched endpoint migrations BUT at the repository layer instead of endpoint layer.

- **Step 4.0 — "Cross-cluster ITenantContext propagation verification":** the analyzer per the canonical spec + a runtime verification mechanism (startup health-check or test) that verifies cross-cluster propagation is correct. Defense-in-depth on top of the compile-time analyzer.

**ONR's read of the directive vs ADR 0091 R2:** the directive's framing is a SUPERSET of the canonical spec. This research covers both interpretations:
- Step 3 (test migration) per ADR + Step 3.0 (consumption sweep across repos) per directive
- Step 4 (analyzer + `[Obsolete]`) per ADR + Step 4.0 (runtime propagation verification) per directive

> **Fold A (.NET-architect verdict 2026-05-21T1228Z — Hidden Concern #2 resolution):**
> This research **implicitly settles the canonical-vs-directive framing as a single
> deliverable**. Step 2.1+ encompasses BOTH endpoint signature narrowing (canonical
> spec) AND repository-internal consumption narrowing (directive's "consumption
> sweep across all repository implementations"). The narrowing decision at each
> consumer site (endpoint or repository) is the single unit of work; whichever
> framing motivates the decision, the deliverable shape is identical:
> 1. Identify which interface variant (facade / `Foundation.MultiTenancy.ITenantContext` / `ICurrentUser` / `IAuthorizationContext`) the consumer actually reads
> 2. Narrow the injected interface to that variant
> 3. Update the corresponding test fixtures (Step 3) to mock the narrowed variant
> 4. The compile-time analyzer (Step 4) enforces non-mixing across data-plane vs control-plane variants regardless of consumer layer
>
> Engineer can treat the canonical and directive framings as equivalent for purposes of work decomposition.

Open question 1 in §6 (Step 3.0 canonical vs directive framing) is therefore **RESOLVED via fold**; remaining open questions in §6 are unaffected.

---

## 3. ITenantContext consumer inventory (verified 2026-05-21T11:55Z)

Grep against `packages/*/` for `Sunfish.Foundation.Authorization` (facade) and `Sunfish.Foundation.MultiTenancy` (narrowed) project references.

### 3.1 Facade consumers (9 packages reference `Sunfish.Foundation.Authorization`)

| Package | Notes |
|---|---|
| `blocks-financial-payments` | Cohort-2 PR 0c migrated repos to tenant-keyed contract; service-layer Option A guards apply. Likely Step 3 target — narrow to `Foundation.MultiTenancy.ITenantContext` if service doesn't read user/roles. |
| `blocks-financial-ledger` | Cohort-2 PR 0d added `IJournalStore` tenant-keyed. Same narrow-to-MultiTenancy candidate. |
| `blocks-financial-ar` | Cohort-2 PR 0a Invoice tenant-keyed. Same. |
| `blocks-financial-ap` | Cohort-2 PR 0b Bill tenant-keyed. Same. |
| `blocks-leases` | Pre-cohort-2 substrate. `ILeaseService.ListAsync` referenced from cohort-1 PR 2; tenant-keying retrofit candidate per W#75 hand-off. |
| `blocks-businesscases` | EntitlementSnapshotBlock.razor reference. Likely reads user + roles for entitlement rendering — KEEP facade until consumption-sweep audit. |
| `blocks-subscriptions` | Subscription operations may need user + roles for billing-side decisions — KEEP facade until audit. |
| `foundation` tests | NotificationContractsTests — likely tests use the facade for mock construction. Step 3 (test migration) updates the mock to the narrowed interface where appropriate. |
| `foundation-authorization` tests | Step 1 acceptance tests; remain unchanged through Steps 3+4. |

### 3.2 Narrowed `Foundation.MultiTenancy` consumers (6 packages)

| Package | Notes |
|---|---|
| `blocks-financial-payments` | Both — facade + narrowed. Cohort-2 PR 0c uses the narrowed surface for `_capturedTenantId` per Option A; facade for the service's authz checks. |
| `blocks-financial-ledger` | Same — both. |
| `foundation-channels` | Channel resolution by tenant; likely Tenant-only consumer (narrow per audit). |
| `foundation-wayfinder` | OodWatch + StandingOrder — checks tenant for routing decisions; user-agnostic. Narrow candidate. |
| `blocks-engine-room` | Observability — narrowed; reads tenant for emission. |
| `foundation-quarterdeck` | DefaultQuarterdeckDataProvider — reads tenant for entry-point data. Narrow candidate. |

### 3.3 Bridge-side consumers (signal-bridge/ — informational)

Bridge handlers + middleware reference `ITenantContext` for auth-policy resolution + endpoint-handler tenant binding. Cohort-1 + cohort-2 endpoints use the facade today; Step 2.1+ batched migrations narrow each endpoint to the minimum it actually reads.

### 3.4 Step 3 + Step 4 implications

**Test migration (canonical Step 3):**
- Each package's `tests/` updates `DemoTenantContext` constructions (or test-double constructions) to use the right interface variant matching the production consumer's narrowing decision.
- ~12 packages × ~2-5 test files each = ~30-60 test fixture updates total.
- Engineer effort: 1-2h per package × 12 = 12-24h. Likely 6-8 batched PRs by package proximity.

**Consumption sweep (directive's Step 3.0):**
- Audit each facade consumer to identify which can narrow to a single interface (mostly `Foundation.MultiTenancy.ITenantContext` for tenant-only consumers; rarely `ICurrentUser`-only or `IAuthorizationContext`-only).
- For each narrowing decision, the test-fixture migration follows.
- Engineer effort: same envelope as test migration (the audit + narrowing IS the Step 2.1+ work; Step 3 follows).

---

## 4. Step 4 analyzer design (`RequestContextMixingAnalyzer`)

### 4.1 Analyzer purpose (per ADR 0091 R2 amendment 6 / A2)

The MUST-NOT-mix-pipelines invariant between `IBrowserTenantContext` (data-plane, subdomain-resolved, crypto-primitive-bearing) and the control-plane interfaces (`Foundation.Authorization.ITenantContext` facade + the three narrowed interfaces) must be compile-time enforceable.

Per ADR 0091 R2 §"Substrate / layering notes":

> `Sunfish.Bridge.Middleware.IBrowserTenantContext` is unchanged — data-plane only, subdomain-resolved, crypto-primitive-bearing, MUST-NOT-mix with the control-plane contexts (invariant preserved by structure today; analyzer-enforced in Step 4 per amendment A2).

### 4.2 Precedent: `foundation-wayfinder-analyzers`

Per ADR 0091 R2 §"Step 4 — facade [Obsolete] + analyzer ship":

> Ship `Sunfish.Foundation.Authorization.Analyzers.RequestContextMixingAnalyzer` (Roslyn) following the existing `foundation-wayfinder-analyzers` pattern.

Need to verify the wayfinder-analyzers pattern at Engineer pre-flight. Likely:
- Separate analyzer project: `Sunfish.Foundation.Authorization.Analyzers.csproj` (analyzer-only; `Microsoft.CodeAnalysis.CSharp` dependency)
- Per-rule diagnostic ID: `SUNFISH_AUTH_001` (or similar; mirror provider-neutrality `SUNFISH_PROVNEUT_001`)
- Severity: Error (per "fails closed")
- Bundled into a NuGet package or distributed as an analyzer reference

### 4.3 Detection scope

The analyzer scans:
- DI registration sites (`services.AddScoped<...>`, `services.AddSingleton<...>`, etc.)
- Endpoint handler signatures (constructor parameters; method parameters with `[FromServices]`)
- Class field declarations (e.g., `private readonly ITenantContext _ctx;`)

Fails when ANY of:
- A class C is registered as both `IBrowserTenantContext` AND any of {`Foundation.Authorization.ITenantContext`, `Foundation.MultiTenancy.ITenantContext`, `ICurrentUser`, `IAuthorizationContext`}
- A class C has constructor parameters of BOTH `IBrowserTenantContext` AND any of the above
- A handler method has parameters of BOTH

### 4.4 Implementation pseudo-code

> **Fold B (.NET-architect verdict 2026-05-21T1228Z — Hidden Concern #1 resolution):**
> The analyzer detection mechanism MUST use **symbol resolution** via the
> `SemanticModel` (`SemanticModel.GetSymbolInfo(typeSyntax).Symbol` resolved to
> `INamedTypeSymbol`), NOT syntax-only walking of `IdentifierNameSyntax` or
> `QualifiedNameSyntax` nodes.
>
> **Why:** the syntax-only approach false-negatives on consumers that import
> via `using Sunfish.Foundation.Authorization;` (the dominant style in the
> existing consumer base) — only the unqualified identifier `ITenantContext`
> appears at the use site, with no syntactic signal of which `ITenantContext`
> namespace it resolves to. Symbol resolution against `SemanticModel`
> deterministically yields the full type (`Sunfish.Foundation.Authorization.ITenantContext`
> vs `Sunfish.Foundation.MultiTenancy.ITenantContext` vs
> `Sunfish.Bridge.Middleware.IBrowserTenantContext`) regardless of `using`
> directive shape.
>
> The pseudo-code below is illustrative; Engineer's analyzer implementation
> MUST resolve `INamedTypeSymbol` per-parameter-type before applying the
> mix-detection invariant. See `foundation-wayfinder-analyzers` for the
> canonical symbol-resolution pattern.

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RequestContextMixingAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SUNFISH_AUTH_001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Cross-pipeline tenant-context mixing",
        "Type '{0}' references both IBrowserTenantContext (data-plane) and {1} (control-plane); these must not be mixed in the same pipeline. See ADR 0091 R2 §Substrate notes.",
        "Sunfish.Architecture",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeFieldDeclaration, SyntaxKind.FieldDeclaration);
        // Also scan DI registration extension method calls
    }

    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        // ... iterate parameters; check if both IBrowserTenantContext + control-plane interface present
    }

    // ... AnalyzeMethod, AnalyzeFieldDeclaration similar
}
```

### 4.5 Required test coverage (per analyzer)

```csharp
[Fact]
public async Task DetectsMixing_InConstructor() {
    var source = """
        public class BadHandler {
            public BadHandler(IBrowserTenantContext browser, Foundation.Authorization.ITenantContext ctx) { }
        }
    """;
    var diagnostics = await GetDiagnosticsAsync(source);
    Assert.Single(diagnostics);
    Assert.Equal("SUNFISH_AUTH_001", diagnostics[0].Id);
}

[Fact]
public async Task DetectsMixing_NarrowedInterface() {
    // IBrowserTenantContext + Foundation.MultiTenancy.ITenantContext (narrowed control-plane)
}

[Fact]
public async Task DetectsMixing_InMethodSignature() {
    // Endpoint handler method with [FromServices] params of both interfaces
}

[Fact]
public async Task DoesNotFire_DataPlaneOnly() {
    // Class with IBrowserTenantContext only — no diagnostic
}

[Fact]
public async Task DoesNotFire_ControlPlaneOnly() {
    // Class with Foundation.Authorization.ITenantContext only — no diagnostic
}

[Fact]
public async Task DoesNotFire_SeparateClassesSameCompositionRoot() {
    // CANONICAL Bridge wiring per fold C (see below):
    // services.AddScoped<IBrowserTenantContext, BrowserTenantContext>();
    // services.AddScoped<Foundation.Authorization.ITenantContext, DemoTenantContext>();
    // Two SEPARATE concrete classes registered side-by-side in Program.cs is the
    // canonical Bridge pattern and MUST NOT fire SUNFISH_AUTH_001.
    var source = """
        public class BrowserTenantContext : IBrowserTenantContext { /* impl */ }
        public class DemoTenantContext : Foundation.Authorization.ITenantContext { /* impl */ }
        // In Program.cs:
        // services.AddScoped<IBrowserTenantContext, BrowserTenantContext>();
        // services.AddScoped<Foundation.Authorization.ITenantContext, DemoTenantContext>();
    """;
    var diagnostics = await GetDiagnosticsAsync(source);
    Assert.Empty(diagnostics);  // analyzer must NOT false-positive
}
```

> **Fold C (.NET-architect verdict 2026-05-21T1228Z — Q2 verdict refinement):**
> The previous draft included a sixth test `DetectsMixing_InDIRegistration`
> that fired when both `IBrowserTenantContext` and a control-plane
> `ITenantContext` variant were registered in the same `services` container.
> Per the .NET-architect council Q2 verdict, this test is **REPURPOSED as a
> NEGATIVE-case test** (`DoesNotFire_SeparateClassesSameCompositionRoot`).
>
> **Why:** the canonical Bridge wiring (per `signal-bridge/Program.cs` + the
> cohort-1/2 endpoint composition pattern) registers two SEPARATE concrete
> classes side-by-side: a `BrowserTenantContext : IBrowserTenantContext`
> (data-plane) and a `DemoTenantContext : Foundation.Authorization.ITenantContext`
> (control-plane). Both contexts coexisting in the same DI container is
> structurally correct — the invariant violation only occurs when a SINGLE
> class binds BOTH interfaces (which the constructor/method/field tests
> already cover).
>
> An analyzer that fires on side-by-side registration would false-positive on
> EVERY Bridge composition root, blocking all builds. The repurposed negative
> test pins this behavior.

Minimum: 6 analyzer tests (3 positive: constructor / narrowed-interface / method-signature; 3 negative: data-plane-only / control-plane-only / separate-classes-same-composition-root).

### 4.6 `[Obsolete]` attribute placement

Add `[Obsolete]` to `packages/foundation-authorization/ITenantContext.cs`:

```csharp
namespace Sunfish.Foundation.Authorization;

/// <summary>
/// Transitional sum-interface facade. Preserves source compatibility for the
/// 14 legacy consumers AND forces the concrete impl to provide all three
/// new interfaces coherently.
/// </summary>
/// <remarks>
/// [Obsolete] in Step 4 — narrowed consumers should inject one of:
/// - Foundation.MultiTenancy.ITenantContext (for tenant-only consumers)
/// - Foundation.Authorization.ICurrentUser (for caller-identity-only consumers)
/// - Foundation.Authorization.IAuthorizationContext (for authz-only consumers)
/// Use AddSunfishTenantContext&lt;TConcrete&gt; for DI registration. Facade
/// to be removed in Step 5 after one-cohort grace.
/// </remarks>
[Obsolete(
    "Inject one of Foundation.MultiTenancy.ITenantContext, ICurrentUser, IAuthorizationContext " +
    "instead of the facade. Register via AddSunfishTenantContext<TConcrete>. " +
    "Facade removal scheduled for Step 5 (one-cohort grace from Step 4). See ADR 0091 R2.")]
public interface ITenantContext
    : Sunfish.Foundation.MultiTenancy.ITenantContext,
      ICurrentUser,
      IAuthorizationContext
{
    /// <summary>
    /// Legacy string TenantId. Default-implemented; delegates to Tenant?.Id.ToString().
    /// </summary>
    string TenantId => Tenant?.Id.ToString() ?? string.Empty;
}
```

Each remaining facade consumer will see the obsolete warning at compile time; severity `Warning` (NOT Error) to allow gradual migration during the one-cohort grace window. After grace window + Step 5 deletion, consumers that haven't migrated will fail to compile (CS0246).

---

## 5. Propagation verification (directive's Step 4.0)

Beyond the compile-time analyzer, the directive's Step 4.0 framing suggests RUNTIME verification. Two patterns:

### 5.1 Startup health-check

Extend `TenantContextScopeAssertion.cs` (existing IHostedService per amendment A1) to verify NO cross-cluster mixing has been registered:

```csharp
public sealed class CrossClusterMixingAssertion : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CrossClusterMixingAssertion> _logger;

    public CrossClusterMixingAssertion(IServiceProvider services, ILogger<CrossClusterMixingAssertion> logger)
    {
        _services = services;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct)
    {
        // Scan all registered services for IBrowserTenantContext registrations
        // For each, verify it's NOT registered as a control-plane interface (facade or narrowed)
        // If detected: throw at startup. Fail-closed.
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### 5.2 Integration test (canonical pattern)

A dedicated integration test in `Sunfish.Bridge.Tests.Integration` (or per-cluster) that builds the full DI container + asserts no cross-cluster mixing. Runs in CI; fails the build if a future PR introduces mixing that the analyzer missed.

### 5.3 Council requirement

The directive's "cross-cluster propagation verification" is sec-eng-relevant — the IBrowserTenantContext/control-plane mix is the security-critical invariant. Sec-eng SPOT-CHECK on Step 4 PR is mandatory per the analyzer's security relevance.

---

## 6. Open questions

For Admiral routing per `feedback_onr_questions_via_inbox`:

### For .NET-architect council

1. ~~**Step 3.0 framing — canonical (test migration only) vs directive (consumption sweep across all repository implementations)?**~~ **RESOLVED via fold A** (§2.3): canonical and directive frame the same deliverable; treat as single unit of work.
2. ~~**Analyzer detection scope — DI registration mixing as Error vs Warning?**~~ **RESOLVED via fold C** (§4.5): separate-classes-same-composition-root is canonical Bridge wiring; analyzer must NOT fire (negative test pins this). Only same-class binding both is Error.
3. **`[Obsolete]` severity — Warning (gradual migration; ONR recommended) vs Error (force migration in Step 4 PR)?** ONR recommends Warning; allows the grace window to actually serve as a window. **(Still open; routes to .NET-architect re-attest)**

Folds applied per .NET-architect council verdict 2026-05-21T1228Z:
- **Fold A** — Hidden Concern #2 (canonical-vs-directive framing as single deliverable) — applied to §2.3
- **Fold B** — Hidden Concern #1 (analyzer must use semantic-model symbol resolution, not syntax-only walking) — applied to §4.4
- **Fold C** — Q2 verdict refinement (§4.5 test #6 repurposed as negative case) — applied to §4.5

### For security-engineering council

1. **Runtime propagation verification — IHostedService startup assertion + integration test (ONR recommended; defense-in-depth) vs analyzer only?** Confirm both.
2. **Test coverage for `RequestContextMixingAnalyzer` — minimum 6 tests per §4.5; confirm or expand.**
3. **`IBrowserTenantContext` redirect — the analyzer flags mixing; should it ALSO flag direct `IBrowserTenantContext` use outside its intended layer (e.g., a control-plane endpoint that injects IBrowserTenantContext)?** ONR's read: scope creep; out of Step 4. Future analyzer extension.

### For CIC

1. **Step 5 facade deletion timing — one cohort post-Step 4 (per ADR 0091 R2) — what defines "one cohort" for accounting purposes? Cohort-3? A subsequent cohort?**

---

## 7. Risks (Engineer focus)

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Step 2.0 hasn't merged when Step 3 PR opens | High | Medium (Step 3 blocked) | Step 3 + 4 research authoring is unblocked NOW; implementation gated on Step 2.0 + 2.1+ |
| Analyzer false-positive on `Sunfish.Bridge.Tests` test doubles that legitimately need both interfaces for cross-cluster integration tests | Medium | Low (test annotation fix) | `[SuppressMessage("Sunfish.Architecture", "SUNFISH_AUTH_001", Justification = "...")]` on test-only fixtures |
| `foundation-wayfinder-analyzers` pattern doesn't match what's actually shipped | Low | Medium (analyzer design rework) | Engineer verifies at pre-flight; ONR's pseudo-code is structural, not literal |
| Consumer audit (Step 3.0 sweep) reveals consumers that need facade beyond Step 5 | Low | High (delays facade deletion) | Audit during research phase; flag any consumer whose narrowing is genuinely impossible |
| `[Obsolete]` warning floods CI on facade consumers that haven't migrated | High | Low (acceptable; signals progress) | Migrate in cohort-N+ work; warning is the FORCING function |
| Test-fixture migration breaks tests that rely on DemoTenantContext's facade-flavored construction | Medium | Medium (test refactor cost) | Step 3 PR explicitly updates fixtures alongside production-code narrowing |
| Cross-cluster runtime verification false-positive in dev (mixed providers for sec-eng test setup) | Low | Low (suppress in dev environments) | `IsDevelopment()` gating on the IHostedService assertion |

---

## 8. Sources cited

### Primary sources

1. `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` Rev 2 (Accepted 2026-05-19T02:40Z) — §"Phases" Step 3 + Step 4 spec; §"Step 1 PR readiness checklist"; §"Step 2 PR readiness checklist".
2. `shipyard/packages/foundation-authorization/` post-Step-1 (verified 2026-05-21T11:55Z) — ITenantContext + ICurrentUser + IAuthorizationContext + AddSunfishTenantContext helper + IHostedService assertion.
3. `coordination/inbox/admiral-directive-2026-05-21T09-15Z-onr-v2-batch-research-queue.md` item #1 — parent directive (Step 3.0 + 4.0 broader framing).
4. ITenantContext consumer survey via grep `Sunfish.Foundation.Authorization` + `Sunfish.Foundation.MultiTenancy` across `packages/` (2026-05-21T11:55Z).

### Secondary sources

5. `shipyard/icm/01_discovery/research/adr-0091-step-2-0-dbcontext-rewrite-research-2026-05-20.md` (shipyard#56 — V1 #3 research) — Step 2.0 implementation pre-research; sequencing predecessor.
6. `coordination/inbox/admiral-ruling-2026-05-18T03-55Z-onr-adr-0091-consolidated-council-amendments.md` — consolidated amendments (1 + 6 + A1-A5 relevant to Step 4 analyzer + DI helper).
7. Cohort-2 PR 0 cluster merges (shipyard#52, #57, #60, #64) — financial-cluster tenant-keyed repository contract precedent.
8. shipyard#44 (ADR 0091 Step 1 — extract Authorization package + sum-interface facade) — Step 1 MERGED.

### Tertiary sources

9. Microsoft Roslyn analyzer documentation — `DiagnosticAnalyzer` base class + `AnalysisContext` registration patterns.
10. `foundation-wayfinder-analyzers` pattern reference (not deeply inspected; Engineer verifies at pre-flight).
11. ASP.NET Core IHostedService pattern (canonical for startup assertions).

---

## 9. What ONR does next

Returns to V2 research queue. Per proceed-continuously discipline:

- Item #1 deliverable complete (this doc + status beacon).
- File `onr-status-*-research-queue-v2-item-1-adr-0091-step-3-4-complete.md` (3 open questions surfaced inline per `feedback_questions_via_inbox`).
- Proceed to V2 #2: ADR 0092 Step 2.0 EFCore tenant-keyed query operator pre-research.

— ONR, 2026-05-21T11:58Z
