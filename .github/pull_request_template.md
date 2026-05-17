## Summary

<!-- What does this PR do? One or two sentences. -->

## ICM Stage

<!-- Which pipeline stage does this work come from? -->
- [ ] This PR has a corresponding ICM stage output in `/icm/`
- [ ] Accelerated / no ICM stage (explain below)

ICM stage: `<!-- e.g. 06_build — feat/my-feature -->`

## Affected Packages

<!-- Check all that apply -->
- [ ] `packages/foundation`
- [ ] `packages/foundation-*` (split packages)
- [ ] `packages/ui-core`
- [ ] `packages/ui-adapters-blazor`
- [ ] `packages/ui-adapters-react`
- [ ] `packages/compat-telerik`
- [ ] `packages/blocks-*`
- [ ] `apps/` (docs / kitchen-sink / local-node-host)
- [ ] `tooling/`
- [ ] Cross-repo impact on Harborline-Software/sunfish or Harborline-Software/signal-bridge (link the matching PR)
- [ ] Repo infrastructure / CI / docs only

## Checklist

- [ ] Build passes (`dotnet build Shipyard.slnx`)
- [ ] Tests pass (`dotnet test Shipyard.slnx`)
- [ ] No Blazor/framework types in `packages/foundation`
- [ ] Public API changes are XML-documented
- [ ] User-facing changes include kitchen-sink demo update
- [ ] User-facing changes include docs update
- [ ] `compat-telerik` impact considered (if applicable)
- [ ] Adapter parity maintained (Blazor + React, if applicable)
- [ ] Cross-repo PRs in sunfish / signal-bridge linked (when API surface changes affect consumers)
