---
title: Standing-Approved Patterns Catalog
status: ratified
authored-by: XO
authored-at: 2026-05-17T11-45Z
ratified-by: CO
ratified-at: 2026-05-17T12-00Z
effective: 2026-05-17
related-rulings:
  - xo-ruling-workstream-pre-authorization (ratified 2026-05-17; see `_shared/engineering/xo-ruling-workstream-pre-authorization.md`)
applies-to: Sunfish only (NOT Galley — per CO ratification scope)
review-cadence: monthly (XO + CO)
---

# Standing-Approved Patterns Catalog

## Purpose

When a PR matches a **standing-approved pattern** in this catalog, the standard PR pipeline shortens:

- **No mandatory council subagent dispatch** (pattern is proven safe; council ran ≥3 times on prior pattern PRs and consistently returned APPROVED with mechanical-only amendments)
- **No CO approval click required** (CO has pre-authorized this pattern's PRs)
- **Auto-merge fires** on CI-green + pattern-conformance verification

The catalog is a **trust-amortization mechanism.** The first 3 PRs of a pattern bear full council + CO review cost. After 3 successful shippings with no post-merge incidents, XO proposes the pattern for catalog entry. CO ratifies. Subsequent matching PRs flow automatically.

## How a PR matches a pattern

cob/dev adds a single line to PR description:

```
@standing-pattern: <pattern-id>
```

XO (or a future automated checker) verifies the PR's changeset matches the pattern's "matches" criteria. If it does, the PR is eligible for the short pipeline. If the changeset deviates beyond the pattern's bounds, the PR falls back to the full pipeline (and XO files a `deviation-from-pattern` note in the PR comment so CO sees it on the next digest).

## Pattern entry shape

Each entry below has:
- **Pattern ID** (kebab-case stable identifier)
- **Definition** (what the pattern is)
- **Matches** (objective criteria — file paths, change types, additive constraints)
- **Does NOT match** (negative criteria — what disqualifies a PR)
- **Shipping history** (PRs that established the pattern; minimum 3 to ratify)
- **Council requirement** (typically SKIP for standing patterns; some retain a single light-weight check)
- **Revoke conditions** (what triggers removing the pattern from the catalog)

---

## Catalog

### `pattern-001` — Cluster scaffold + Repository + DI

**Definition:** First PR in a new `blocks-*` cluster — creates the package skeleton, defines core entities + IDs + status enums, scaffolds `I<Thing>Repository` + `InMemory<Thing>Repository`, registers DI via `Add<Cluster>()` extension, lands tests for repository + entity invariants.

**Matches when:**
- Touches files matching: `packages/blocks-<new-name>/**` (greenfield) OR `packages/foundation-<new-name>/**`
- Adds files: `Sunfish.Blocks.<X>.csproj`, `README.md`, `NOTICE.md`, `Models/<Entity>.cs`, `Models/<Entity>Id.cs`, `Models/<Entity>Status.cs`, `Services/I<Entity>Repository.cs`, `Services/InMemory<Entity>Repository.cs`, `DependencyInjection/<X>ServiceCollectionExtensions.cs`, `tests/<X>.Tests.csproj`, `tests/<Entity>Tests.cs`
- Adds the package + test project to `Sunfish.slnx`
- Test count: 15-50 new tests
- No edits to existing `blocks-*` or `foundation-*` source files (additive only)

**Does NOT match if:**
- Touches `accelerators/`, `apps/`, `tooling/` (outside scope)
- Touches existing cluster source files (this is the SCAFFOLD pattern, not an EXTENSION pattern)
- Introduces a new public DI surface beyond `Add<Cluster>()`
- Includes any `foundation-security-policy/**` or `kernel-audit/**` changes (security surface — needs full pipeline)

**Shipping history:**
- PR #934 (`blocks-people-foundation` PR 1)
- PR #946 (`blocks-financial-ar` PR 1)
- PR #921 (`blocks-work-orders` PR 1)
- PR #933 (`blocks-work-projects` PR 1)
- PR #959 (`blocks-financial-ap` PR 1)
- All ≥3 successful shippings; no post-merge incidents.

**Council requirement:** SKIP. Pattern is well-established; substrate scaffold has no novel security surface.

**Revoke if:**
- Any post-merge incident on a pattern-matched PR
- 3 consecutive pattern-matched PRs require XO to file a `deviation-from-pattern` note
- A new security or compliance surface is added to the pattern (e.g., encryption, auth) — then pattern must be re-ratified

---

### `pattern-002` — ERPNext importer Pass 1 (independent foundational data)

**Definition:** Per-cluster ERPNext importer for Pass 1 data (accounts, fiscal years, tax codes, parties, etc.) — entities with no inter-cluster FK dependencies. Implements `IErpnext<Thing>Importer` + `Erpnext<Thing>Importer` + `Erpnext<Thing>Source` + `ImportOutcome<T>` + idempotent upsert via `ExternalRef == source.Name` lookup.

**Matches when:**
- Touches files matching: `packages/blocks-<cluster>/Migration/**` only
- Adds files: `IErpnext<X>Importer.cs`, `Erpnext<X>Importer.cs`, `Erpnext<X>Source.cs`, `ImportAction.cs` (if new), `ImportOutcome.cs` (if new), `tests/Erpnext<X>ImporterTests.cs`
- Importer signature matches: `Task<ImportOutcome<T>> UpsertFromErpnextAsync(Erpnext<X>Source source, <TargetContext>? target, CancellationToken ct = default)`
- Test count: 8-20 new tests (covers add, skip, version-older-than-source, error, idempotent re-run)

**Does NOT match if:**
- Importer is Pass 2 or Pass 3 (FK-dependent on other cluster data — needs spec-conformance review)
- Touches non-`Migration/` files in the cluster
- Adds new dependency on another cluster (additive references break the "independent" invariant)

**Shipping history:**
- PR #902 (`blocks-financial-ledger` `ErpnextAccountImporter` + `ErpnextJournalEntryImporter` — note: this PR shipped both Pass 1 + Pass 3; the Pass 1 substrate qualifies)
- PR #904+ #907+ #920 (`blocks-financial-tax` ErpnextTaxImporter)
- PR #919 (`blocks-financial-periods` ErpnextFiscalYear + ErpnextFiscalPeriod importers)
- PR #943 (`blocks-people-foundation` ErpnextPartyImporter)
- PR #940 (`blocks-work-projects` ErpnextProjectImporter)
- ≥5 successful shippings; consistent shape.

**Council requirement:** SKIP. Pattern is well-established; no security-sensitive surface.

**Revoke if:**
- Importer adds a write path (creates ERPNext records — not idempotent-upsert direction)
- Importer introduces a new authentication or secret-handling surface
- Post-merge incident on a pattern-matched PR

---

### `pattern-003` — Cluster posting service (`I<X>PostingService`)

**Definition:** Cluster's main write-path service that implements state-machine transitions on the cluster's core entity (e.g., `IInvoicePostingService` flips Invoice Draft → Issued → Void/WrittenOff; `IBillPostingService` does Received → PartiallyPaid → Paid). Atomic JE posting on state transition. Cancels-tracked transitions. Emits canonical events.

**Matches when:**
- Touches files matching: `packages/blocks-<cluster>/Services/<X>PostingService.cs`, `packages/blocks-<cluster>/Services/I<X>PostingService.cs`, `packages/blocks-<cluster>/Models/Events/**` (new event records)
- Adds tests at `packages/blocks-<cluster>/tests/<X>PostingServiceTests.cs`
- Service is registered via `Add<Cluster>()` extension as `TryAddSingleton<I<X>PostingService, <X>PostingService>`
- Uses canonical `IDomainEventPublisher` for event emission (NOT a local stub)
- Test count: 15-30 new tests (covers happy-path transitions, invalid transitions, cancellation, event emission)

**Does NOT match if:**
- Service introduces a new auth surface (e.g., role-based gates beyond the cluster's existing pattern)
- Service touches `foundation-security-policy/**` or `kernel-audit/AuditEventType.cs`
- Service introduces a new tax-calculation, currency-conversion, or other financial-correctness primitive beyond the cluster's existing `ITax<>` shim

**Shipping history:**
- PR #899 (`blocks-financial-ledger` `postJournalEntry` atomic posting)
- PR #952 (`blocks-financial-ar` `IInvoicePostingService`)
- PR #960 (`blocks-financial-ap` `IBillPostingService`)
- 3 successful shippings; consistent shape.

**Council requirement:** **LIGHT** — single-perspective spot-check by Sonnet (medium effort) confirming event-emission shape matches the cross-cluster event-bus catalog. Other perspectives skipped.

**Revoke if:**
- Posting service introduces a new financial-correctness primitive
- Council spot-check finds a deviation in event-emission shape

---

### `pattern-004` — Cluster aging service (`I<X>AgingService`)

**Definition:** Read-side cluster service that buckets open transactions into aging windows (current / 1-30 / 31-60 / 61-90 / 90+) by tenant + per-party + per-property. Pure projection over existing repository state.

**Matches when:**
- Touches files matching: `packages/blocks-<cluster>/Services/<X>AgingService.cs`, `packages/blocks-<cluster>/Services/I<X>AgingService.cs`, `packages/blocks-<cluster>/Models/<X>AgingBucket*.cs` (if new aggregate)
- Adds tests at `packages/blocks-<cluster>/tests/<X>AgingServiceTests.cs`
- Bucket boundaries match the canonical 5-bucket scheme (current / 1-30 / 31-60 / 61-90 / 90+)
- Per-party + per-property breakdowns implemented
- Test count: 8-15 new tests

**Does NOT match if:**
- Bucket scheme deviates from the canonical 5-bucket
- Service introduces a write path
- Touches files outside `Services/` and `Models/` of the cluster

**Shipping history:**
- PR #955 (`blocks-financial-ar` `IArAgingService`)
- PR #961 (`blocks-financial-ap` `IApAgingService`)
- 2 successful shippings — **NOT YET RATIFIED** (needs ≥3). Pattern is proposed; current ratification gate is "after one more aging service ships."

**Council requirement:** SKIP (once ratified).

**Revoke if:**
- A future aging service is needed (e.g., for cash-flow projections) but the canonical bucket scheme is wrong for the use case

---

### `pattern-005` — DI extension `Add<Block>()` umbrella

**Definition:** A cluster's umbrella DI registration that composes all sub-services + repositories + event publishers + options into a single `services.Add<ClusterName>()` call. Always uses `TryAddSingleton` for swappable surfaces and `AddSingleton` for non-replaceable invariants.

**Matches when:**
- Touches a single file: `packages/blocks-<cluster>/DependencyInjection/<ClusterName>ServiceCollectionExtensions.cs`
- Adds at most 1 new public extension method
- Test changes (if any) only validate registration order / lifetime
- Test count: 3-8 new tests

**Does NOT match if:**
- Introduces a new `services.Replace<>()` call (overrides someone else's registration — needs review)
- Adds a new options class (`AddOptions<>` is fine; defining the new options shape may need review)
- Pulls in a new cross-cluster reference (csproj edit)

**Shipping history:**
- PR #924 (`blocks-work-orders` `AddBlocksWorkOrders`)
- PR #940 (`blocks-work-projects` `AddBlocksWorkProjects`)
- PR #941 (`blocks-people-foundation` `AddBlocksPeopleFoundation`)
- ≥3 successful shippings.

**Council requirement:** SKIP.

**Revoke if:**
- A registration introduces a singleton with state that's not thread-safe (caught by .NET architect spot-check on first incident)

---

### `pattern-006` — `apps/docs/blocks/<cluster>/overview.md` authoring

**Definition:** Per-cluster documentation page in the `apps/docs` site. Single Markdown file describing the cluster's purpose, public surface, consumers, and example wire-up.

**Matches when:**
- Touches files matching: `apps/docs/blocks/<cluster>/**.md` ONLY
- May also touch `apps/docs/blocks/toc.yml` to add a TOC entry
- No code changes
- No test changes

**Does NOT match if:**
- Touches any `.cs`, `.csproj`, `.ts`, `.tsx`, `.json` files
- Modifies an existing docs page that's been accepted by CO (use `pattern-008` instead)

**Shipping history:**
- PR #366 (`blocks-leases` docs)
- PR #315 (`blocks-maintenance` docs)
- PR #398 (multiple block READMEs)
- ≥3 successful shippings.

**Council requirement:** SKIP.

**Revoke if:** N/A (pure docs).

---

### `pattern-007` — Ledger row flip (`active-workstreams.md` state change)

**Definition:** Single-row update to `icm/_state/active-workstreams.md` (or the workstream source `W*.md` file + render-ledger.py output) reflecting state transition (e.g., `building` → `built`, `ready-to-build` → `building`).

**Matches when:**
- Touches files matching: `icm/_state/workstreams/W*.md` (source) + `icm/_state/active-workstreams.md` (rendered) — both files together
- No other file changes
- Row update is a `State:` line modification only (not adding new content)

**Does NOT match if:**
- Adds a NEW workstream row (use full pipeline; CO must see new workstreams)
- Modifies hand-off file (use `pattern-008` for hand-off updates)
- Touches `icm/_state/handoffs/`

**Shipping history:**
- Every workstream completion (10+ instances over the past 2 weeks).
- ≥10 successful shippings.

**Council requirement:** SKIP.

**Revoke if:** N/A.

---

### `pattern-008` — Docs page touch-up (existing apps/docs file)

**Definition:** Edits to an existing `apps/docs/**.md` file — typo fixes, clarifications, version bumps in example snippets, adding a missing section.

**Matches when:**
- Touches only `apps/docs/**.md` files (existing files; no new files in scope here — `pattern-006` covers new docs)
- No `.cs`, `.csproj`, `.json` changes
- Net additions ≤ 200 lines, net deletions ≤ 100 lines

**Does NOT match if:**
- Changes >200 lines net additions
- Touches `apps/docs/blocks/toc.yml` in a way that REMOVES an entry (could break navigation)

**Shipping history:**
- Frequent throughout repo history.
- ≥5 successful shippings.

**Council requirement:** SKIP.

**Revoke if:** N/A.

---

---

### `pattern-009` — Bridge endpoint + companion frontend binding pair

**Definition:** A PR that wires a new cockpit Bridge endpoint together with the matching Anchor/Sunfish React page rebind — the backend endpoint + the frontend binding ship together or in a coordinated cohort. MANDATORY security-engineering council SPOT-CHECK on every instance.

**Matches when:**
- PR adds or extends a cockpit endpoint in `Sunfish.Bridge/Cockpit/` AND rebinds the corresponding frontend page from an ERPNext direct call to the new cockpit route
- OR: a PR explicitly identified as one PR in a cohort where another PR in the same cohort does the pairing (must reference the cohort in PR description with `@standing-pattern: pattern-009`)
- `@standing-pattern: pattern-009` appears in the PR description

**Does NOT match if:**
- Backend endpoint ships without a corresponding frontend rebind (or vice-versa without a cohort framing)
- Any route under `/api/v1/cockpit/identity/**` or `/api/v1/cockpit/auth/**` (identity surface — always full pipeline)
- Diff exceeds 1500 lines net

**CSRF requirement:** ONLY if the endpoint is in the cookie-auth route family. Field route family (`/api/v1/field/*`) uses pairing-token / mTLS — CSRF does NOT apply there.

**Tenant cross-check sub-step (MANDATORY for state-mutating handlers with client-supplied identifiers):** Before any service call referencing a client-supplied entity ID, the handler MUST:
1. Fetch the entity
2. Verify its tenant matches the envelope's tenant
3. Return uniform 404 on either missing-or-wrong-tenant (no 403; do not leak existence)

Lifted from W#23.3 P1 sec-eng amendment A2 (2026-05-19).

**Shipping history (instances):**
1. Cohort-1 PR 1 — Properties page rebind (sunfish initial migration; merged 2026-05-17)
2. Cohort-1 PR 2 — Leases page rebind (sunfish #7 + signal-bridge #7; merged 2026-05-17)
3. Cohort-1 PR 3 — Maintenance page + CSRF + audit emission (sunfish #11 + signal-bridge #11 + shipyard #28; sec-eng GREEN-attested; merged 2026-05-18) ← **promotion trigger**
4. Cohort-1 PR 4 — close-out + ERPNext deprecation (sunfish #13 + shipyard #32; merged 2026-05-18)
5. W#23.3 P1 — Inspections walkthrough (shipyard #35 + signal-bridge #13; AMBER-amended pending; sec-eng SPOT-CHECK 2026-05-19T04:15Z)
6. Cohort-2 PR 1 + PR 2 + PR 3 — Financial cluster (LeaseDetailPage payments + AccountingPage + RentCollectionPage; ready-to-build per W#76 hand-off)

**Status:** **Formal — promoted 2026-05-17** after cohort-1 PRs 1+2+3 shipped clean. Per `admiral-status-2026-05-17T23-30Z-pattern-009-promoted.md`.

**Council requirement:** MANDATORY security-engineering SPOT-CHECK on EVERY instance. SLA: Admiral dispatches within 30 min of DRAFT PR opening (per `fleet-conventions.md` § SPOT-CHECK dispatch SLA added 2026-05-18).

**Revoke if:**
- Any post-merge incident on a pattern-matched PR
- A sec-eng SPOT-CHECK finds a systematic gap across ≥2 instances — pattern must be re-ratified with amended criteria

---

## Patterns proposed but not yet ratified

- **`pattern-004` (Cluster aging service)** — needs 1 more shipping to reach 3-PR ratification minimum
- **`pattern-010` (`apps/docs/blocks/<cluster>/toc.yml` entry)** — usually bundled with `pattern-006`; not a standalone pattern yet
- **`pattern-011` (Cross-cluster event publisher wiring)** — was `pattern-009` candidate; renumbered after pattern-009 slot taken by Bridge endpoint + companion frontend binding. Needs 3 shippings for ratification.

## What's explicitly NOT a standing pattern (always needs full pipeline)

- Anything touching `packages/foundation-security-policy/**`
- Anything touching `packages/kernel-audit/AuditEventType.cs` (new audit constants)
- Anything touching `packages/kernel-security/**`
- Anything touching `docs/adrs/**` (ADRs are first-class governance artifacts)
- Anything touching `accelerators/anchor/` substrate (Anchor is high-stakes per ADR 0088)
- Anything touching `accelerators/bridge/Sunfish.Bridge/Cockpit/**` or `accelerators/bridge/Sunfish.Bridge/Features/Identity/**` (auth surface)
- Anything touching `_shared/product/local-node-architecture-paper.md` (foundational paper)
- Any PR adding a NEW workstream row to `active-workstreams.md`
- Any PR with `merge-tier: co-review` or `merge-tier: co-ruling` in its hand-off frontmatter
- Any PR where the diff exceeds 1500 lines net (large PRs need human eyes regardless of pattern)

## Catalog maintenance

- **XO authority:** XO proposes new patterns + revocations
- **CO authority:** CO ratifies new patterns + can revoke at any time
- **Review cadence:** Monthly XO+CO review; revoke or graduate proposed patterns
- **Incident response:** Any post-merge incident on a pattern-matched PR triggers IMMEDIATE pattern review; XO files a post-mortem within 24h
- **Versioning:** This file is versioned by commit; patterns are not stable across versions — `@standing-pattern: pattern-001` is keyed to whatever pattern-001 is defined as AT THE TIME OF PR MERGE

## Operational metrics (to be tracked starting at ratification)

- Patterns hit per week (per pattern + total)
- Deviation-from-pattern flags filed per week
- PRs auto-merged via standing pattern + ratio to total PRs merged
- Post-merge incidents on pattern-matched PRs (target: 0)
- Pattern revocations (target: 0)
- New patterns proposed per month

## Initial rollout plan

**Phase 1 (immediate after CO approval):**
- Patterns 001, 002, 003, 005, 006, 007, 008 go live (each has ≥3 prior shippings)
- Pattern 004 stays proposed pending 1 more aging-service PR
- cob + dev directive issued: start adding `@standing-pattern: <id>` lines to new PRs where applicable

**Phase 2 (week 2):**
- Soft enforcement — XO checks pattern conformance manually on each PR
- Auto-merge stays manual (XO arms after pattern-conformance verified) — no automation yet

**Phase 3 (week 3+):**
- Automated pattern-conformance checker (GitHub Action calling a small Sonnet-medium classifier per PR)
- Auto-merge fires on pattern-conformance-pass + CI-green

**Phase 4 (week 5+):**
- Quarterly catalog review (CO + XO sit down for 30 min)
- Pattern revocation as needed

## Estimated impact

Based on the past 7 days of merged PRs (~30 cluster-build PRs merged across people-foundation + AR + AP + work-orders + work-projects + financial-tax + financial-periods + financial-ledger):

- ~18 PRs would have matched pattern-001 + pattern-002 + pattern-003 + pattern-005 + pattern-006 + pattern-007 + pattern-008
- ~60% auto-merge rate
- CO would see ~12 PRs instead of ~30
- Each remaining PR is genuinely judgment-requiring (new substrate, deviation, novel surface)

Combined with **workstream pre-authorization** (the companion ruling at `xo-ruling-workstream-pre-authorization-2026-05-17.md`), expected to reach **~70-80% reduction in CO clicks**.
