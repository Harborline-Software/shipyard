# Sunfish.Foundation.Bootstrap.Analyzers

Roslyn analyzer for ADR 0095 Step 3 (`BootstrapAndTenantMutualExclusionAnalyzer`).

Two diagnostics, both Error severity:

| Rule | Title |
|---|---|
| `SUNFISH_BOOTSTRAP001` | Constructor injects both `IBootstrapContext` and a post-tenant context |
| `SUNFISH_BOOTSTRAP002` | Bootstrap endpoint registration is missing `.AllowAnonymous()` |

See [ADR 0095](../../docs/adrs/0095-bootstrap-context.md) §"Decision drivers" + §"Council review" for the rationale and trade-offs.

## Detection model

Purely syntactic (per ADR 0095 Rev 2 / .NET-arch A6). Matches by parameter-type
right-most simple name; does not consult `SemanticModel` for symbol resolution
so the analyzer runs cleanly against projects that haven't compiled yet
(Roslyn's "live build" pipeline). The accepted 20% gap — `IServiceProvider.GetService<T>()`
inside method bodies and factory delegates — remains code-review territory
(same trade-off as ADR 0091 R2 A2's `RequestContextMixingAnalyzer`).

## Layout

This package is the sibling analyzer csproj of `packages/foundation-bootstrap/`
(per the ADR §"Substrate / layering notes" — same shape as
`foundation-wayfinder` ↔ `foundation-wayfinder-analyzers`). Consumers add it via:

```xml
<ProjectReference Include="..\foundation-bootstrap-analyzers\Sunfish.Foundation.Bootstrap.Analyzers.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

or via the NuGet package once published.
