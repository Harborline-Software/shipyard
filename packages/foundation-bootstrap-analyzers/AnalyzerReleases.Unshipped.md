; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SUNFISH_BOOTSTRAP001 | SunfishBootstrap | Error | Constructor injects both IBootstrapContext and a post-tenant context interface, BootstrapAndTenantMutualExclusionAnalyzer (ADR 0095 Step 3)
SUNFISH_BOOTSTRAP002 | SunfishBootstrap | Error | MapBootstrapEndpoints-registered endpoint without .AllowAnonymous(), BootstrapAndTenantMutualExclusionAnalyzer (ADR 0095 Step 3 / sec-eng Gap D)
