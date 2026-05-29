---
id: 97
title: PasswordHasher Substrate
status: Accepted
date: 2026-05-27
proposed-date: 2026-05-27
author: Admiral
tier: foundation
pipeline_variant: sunfish-api-change

concern:
  - password-hashing
  - authentication-credential-storage
  - cryptography
  - production-safety
  - migration-from-pbkdf2

enables:
  - w79-signup-handler-argon2id-cutover
  - w80-login-handler-rehash-on-next-login
  - future-vertical-signup-paths
  - pre-production-hardening-gate

composes:
  - 13   # Foundation.Integrations (provider-neutrality precedent — adapted in spirit, not letter; Argon2id is a primitive, not a vendor)
  - 49   # AuditEvent (forward-watch: `user_password_hash_upgraded` enum-extension candidate at W#80 layer)
  - 91   # ITenantContext Divergence Resolution (R2 / A1 startup-assertion precedent for the Tier-1 mock production-guard)
  - 95   # Bootstrap Context substrate (SignupHandler runs in bootstrap scope; PasswordHasher is bootstrap-context-agnostic)
  - 96   # Tier-2 Vendor-Provider Substrate (cross-tier reuse of the mock-marker + production-guard discipline at Tier-1 boundary)
  - 98   # Block-Naming Generalization (substrate-tier ADR cadence + dual-council MANDATORY precedent)

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

requires-council:
  - dotnet-architect
  - security-engineering

co-pre-authorized: false  # substrate-tier ADR; ADR text + each Step 1/2 implementation PR carries mandatory dual-council per H9 / §"Council review"

amendments: []
---

# ADR 0097 — PasswordHasher Substrate

**Status:** Accepted (Revision 2; dual-council GREEN 2026-05-27: sec-eng + .NET-arch). Rev 1 dual-council AMBER produced 6 sec-eng substrate amendments (S1-S6), 6 .NET-arch load-bearing amendments (A1-A6), and 9 .NET-arch clarifications (C1-C9) — all folded into Rev 2 per the ADR 0095 R2 / 0096 R2 / 0098 R2 precedent; both councils self-attested GREEN on Rev 2.
**Date:** 2026-05-27
**Resolves:** W#79 sub-cohort 1 H8 disposition (`coordination/inbox/admiral-ruling-2026-05-26T0035Z-w79-stage-05-9-halts-resolved.md` §H8) — pre-production hardening commitment that swaps the W#79 MVP-ship `IPasswordHasher<UserEntity>` PBKDF2-HMAC-SHA256 default for an OWASP-canonical Argon2id-backed implementation **before W#79 reaches production**. This ADR is the deferred-with-commitment. Codifies the substrate as a Tier-1 domain-block primitive with cross-tier reuse of ADR 0096's mock-first + production-guard discipline at the Tier-1 boundary. W#79 sub-cohort 1 ships at MVP with PBKDF2 V3 default; W#79 Step 2 (this ADR's Step 2) swaps the composition-root registration to Argon2id pre-production via a zero-handler-tier-change DI substitution.
**Council inputs:** Revision 1 forwards to dual-council. Sec-eng dual-council MANDATORY (per H9 + W#79 H8); .NET-architect dual-council MANDATORY (per H9). Promotion path: both councils self-attest GREEN via inbox status on Revision 1 → Admiral promotes ADR to `Accepted`. If a council returns AMBER, Admiral folds amendments into Revision 2 (ADR 0095 R2 / 0096 R2 / 0098 R2 precedent). **Step 1 (foundation-password-hashing substrate package) PR carries its own mandatory dual-council SPOT-CHECK at PR-open** per H9 — independent council pull on the cryptographic-primitive surface. **Step 2 (W#79 SignupHandler composition-root cutover) PR carries sec-eng MANDATORY SPOT-CHECK** for the substitution-correctness invariant.
**Predecessor research:** `shipyard/icm/01_discovery/research/onr-adr-0097-passwordhasher-substrate-scaffold.md` (1167 lines; ONR; merged via `shipyard#167`); ONR status `coordination/inbox/onr-status-2026-05-27T1428Z-adr-0097-passwordhasher-substrate-scaffold-complete.md`. Scaffold surfaced 11 halt conditions; Admiral 11-halt ruling (`coordination/inbox/admiral-ruling-2026-05-27T1430Z-adr-0097-passwordhasher-11-halts-resolved.md`) RATIFIED all 11 at ONR-recommended defaults in single pass (zero DEFERRED-CIC; zero OVERRIDE).

---

## Revision history

| Rev | Date | Author | Summary |
|---|---|---|---|
| 1 | 2026-05-27 | Admiral | Initial draft. Folds ONR scaffold (Option C — Argon2id via Konscious + retain `IPasswordHasher<TUser>` interface + version-tagged hash output for migration detection + Tier-1 domain-block placement) and Admiral 11-halt ruling (all 11 RATIFY-ONR-RECOMMENDATION). Codifies new `packages/foundation-password-hashing/` substrate package (Halt 3) carrying `Argon2idPasswordHasher<TUser>` + `Argon2idHashOptions` + `MockPasswordHasher<TUser>` + `IMockPasswordHasher` marker + `MockPasswordHasherProductionGuardAssertion` IHostedService + DI helpers `AddSunfishPasswordHashing<TUser>` + `AddSunfishMockPasswordHashing<TUser>`. Argon2id parameters pinned at OWASP minimum m=19456 KiB / t=2 / p=1 (Halt 1). NuGet library: `Konscious.Security.Cryptography.Argon2` v1.3.1 (Halt 2). Mock-first discipline applied at Tier-1 boundary via fresh `IMockPasswordHasher` marker family (Halt 4; distinct from ADR 0096's `IMockVendorProvider` — cross-tier reuse, not interface conflation). Hash output: Konscious PHC string format `$argon2id$v=19$m=...,t=...,p=...$<salt>$<hash>` (Halt 6). Migration: lazy rehash-on-next-login (Halt 5; bulk-rehash impossible by physics — no plaintext retrievable). No pepper at MVP; nullable `Argon2idHashOptions.Pepper` field reserved for future secret-store substrate (Halt 7). Pre-production sequencing: Steps 1-2 merge before W#79 reaches production-customer onboarding (Halt 8). Council cadence: sec-eng + .NET-architect dual-council MANDATORY on ADR text + Step 1; sec-eng MANDATORY SPOT-CHECK on Step 2 (Halt 9). Backward-compat: `Argon2idPasswordHasher.VerifyHashedPassword` delegates legacy V3 PBKDF2 byte[] format to a privately-composed `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` fallback; returns `PasswordVerificationResult.SuccessRehashNeeded` on legacy-hash verify success (Halt 10). Options binding: `IOptions<Argon2idHashOptions>` singleton snapshot — substrate-tier parameter changes ship via deployment, not runtime config reload (Halt 11). Status: Proposed (awaiting dual-council). |
| 2 | 2026-05-27 | Admiral | Dual-council AMBER fold. Folds .NET-architect load-bearing amendments A1-A6: A1 TFM pin `net11.0` (strikes "netstandard2.0 or net9.0+" ambiguity per fleet `Directory.Build.props` convention); A2 CPM addition for `Microsoft.AspNetCore.Identity.Core` to `Directory.Packages.props` (fleet-scope side effect explicit); A3 closed-generic `ServiceDescriptor` reflection idiom (`IsGenericType && GetGenericTypeDefinition() == typeof(IPasswordHasher<>)`) spelled out for the production-guard scan; A4 `services.Replace` (not `AddSingleton`) inside `AddSunfishPasswordHashing<TUser>` per ADR 0096 R2 §D1c amendment #2 precedent — makes Step 2 cutover idempotent; A5 sync-over-async via `Task.Run(() => ...).GetAwaiter().GetResult()` hop (Option B; Microsoft documented safe substrate-tier pattern) — closes Blazor-Server SynchronizationContext deadlock-class hazard; A6 third DI helper `AddSunfishPasswordHashingSubstrate(this IServiceCollection)` per ADR 0096 `AddSunfishVendorProviderSubstrate` precedent — registers `MockPasswordHasherProductionGuardAssertion` + `Argon2idParameterFloorAssertion` IHostedServices exactly once via `TryAddEnumerable`. Folds sec-eng substrate amendments S1-S6: S1 pepper application via Argon2 `KnownSecret` property (RFC 9106 §3.1 `K` parameter; Konscious's native secret-value surface) — XOR-into-password-bytes rejected as non-canonical; S2 `MockPasswordHasher` returns the constant string `"mock-hash"` (no password-derived material; Floor 7 satisfied by construction); S3 new `Argon2idParameterFloorAssertion : IHostedService` enforces Floor 3/4/5 non-substitutable-downward at startup; S4 Konscious supply-chain posture floors — `packages.lock.json` + `RestoreLockedMode` + RFC 9106 §5 reference-vector parity tests (≥3) at Step 1 PR scope; S5 Floor 8 primitive-selection assertion (Argon2id-not-Argon2i-not-Argon2d reflective verification); S6 Floor 6 input-length bounds (`hashedPassword.Length ≤ 1024`, `providedPassword.Length ≤ 4096`, `Pepper.Length ≤ 64`). Folds .NET-arch clarifications C1-C9: `ServiceLifetime` parameter on `AddSunfishPasswordHashing<TUser>` (C1); `init`-only setters on `Argon2idHashOptions` (C2); `IValidateOptions<Argon2idHashOptions>` floor enforcement at bind time (C3); captured-snapshot-post-Build convention cited (C4); `ReadOnlySpan<char>` PHC parsing (C5); `CryptographicOperations.FixedTimeEquals` Span-typed overload (C6); `(int)options.MemoryKib` cast site for Konscious's `MemorySize` API (C7); `RandomNumberGenerator.Fill(Span<byte>)` modern static API (C8); C9 absorbed by S2. Q1 (sync-over-async): A5 Option B Task.Run hop — RESOLVED. Q4 (unified `SuccessRehashNeeded`): CONFIRM unified-trigger treatment; no `PasswordVerificationResultExtended`. Q7 (csproj package-ref set): 5 PackageReferences (`Microsoft.AspNetCore.Identity.Core` + `Konscious.Security.Cryptography.Argon2` + `Microsoft.Extensions.DependencyInjection.Abstractions` + `Microsoft.Extensions.Hosting.Abstractions` + `Microsoft.Extensions.Options`); NU1510 NoWarn per `foundation-authorization` precedent — RESOLVED. Q2/Q3/Q5/Q6 sec-eng-primary resolutions folded into spec text per S1/S2 amendments + open-questions update. Status: Proposed (awaiting dual-council re-attestation). |

Promotion path: both councils self-attest GREEN via inbox status on Revision 1 → Admiral promotes ADR to `Accepted`. If a council returns AMBER (likely — substrate-tier cryptography ADRs invariably surface load-bearing amendments at first attestation per ADR 0095 R2 / 0096 R2 / 0098 R2 precedent), Admiral folds amendments into Revision 2 and re-attests. **Step 1 implementation PR carries its own mandatory dual-council SPOT-CHECK at PR-open** (per H9) — independent council pull on the new cryptographic-primitive substrate surface. **Step 2 implementation PR carries sec-eng MANDATORY SPOT-CHECK** for composition-root cutover correctness (the substitution invariant — zero handler-tier callsite changes — is verifiable by mechanical inspection of the diff).

---

## A0 cited-symbol audit

| Symbol / Path / ADR | Classification | Verified |
|---|---|---|
| `Sunfish.Foundation.PasswordHashing.Argon2idPasswordHasher<TUser>` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.PasswordHashing.Argon2idHashOptions` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.PasswordHashing.MockPasswordHasher<TUser>` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.PasswordHashing.IMockPasswordHasher` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.PasswordHashing.DependencyInjection.MockPasswordHasherProductionGuardAssertion` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.PasswordHashing.DependencyInjection.PasswordHashingServiceCollectionExtensions.AddSunfishPasswordHashing<TUser>` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.PasswordHashing.DependencyInjection.PasswordHashingServiceCollectionExtensions.AddSunfishMockPasswordHashing<TUser>` | Introduced by this ADR | no — added in Step 1 PR |
| `Sunfish.Foundation.PasswordHashing.MockPasswordHasherInProductionException` | Introduced by this ADR | no — added in Step 1 PR |
| `Microsoft.AspNetCore.Identity.IPasswordHasher<TUser>` | Existing (BCL — `Microsoft.AspNetCore.Identity.Core`) | yes — canonical typed interface; preserved as the SignupHandler injection point per W#79 Rev 2 sec-eng A + .NET-arch K1 invariant |
| `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` (default impl) | Existing (BCL); composed privately as legacy-hash verify fallback | yes — `PasswordHasherCompatibilityMode.IdentityV3` — PBKDF2-HMAC-SHA256, 100k iterations, version byte 0x01 |
| `Microsoft.AspNetCore.Identity.PasswordVerificationResult` (enum) | Existing (BCL); `SuccessRehashNeeded` is the migration trigger primitive | yes — load-bearing for the lazy-rehash mechanism per Halt 5 / Halt 10 |
| `Konscious.Security.Cryptography.Argon2id` | Existing (`Konscious.Security.Cryptography.Argon2` NuGet v1.3.1; not yet referenced by any shipyard package) | yes — Argon2 1.3 spec conformant; MIT licensed; OWASP-cheatsheet recommended for .NET (verified via `Konscious.Security.Cryptography.Argon2` NuGet listing 2026-05-27) |
| `Sunfish.Foundation.Integrations.IMockVendorProvider` | Existing (ADR 0096 Step 1) | yes — `shipyard/packages/foundation-integrations/IMockVendorProvider.cs` (cross-tier reuse model, NOT extended at Tier-1; `IMockPasswordHasher` is a distinct marker family per Halt 4 rationale) |
| `Sunfish.Foundation.Integrations.DependencyInjection.MockProviderProductionGuardAssertion` | Existing (ADR 0096 Step 1) | yes — `shipyard/packages/foundation-integrations/DependencyInjection/MockProviderProductionGuardAssertion.cs` (shape precedent for Tier-1 analog; distinct concern — Tier-1 mock detection runs over a different registration shape) |
| `Sunfish.Foundation.Bootstrap.IBootstrapContext` | Existing (ADR 0095 Step 1) | yes — `shipyard/packages/foundation-bootstrap/IBootstrapContext.cs` (SignupHandler runs in bootstrap scope; PasswordHasher is bootstrap-context-agnostic — the hash computation does not depend on tenant identity) |
| W#79 `SignupHandler` (cluster: `Sunfish.Bridge.Onboarding`) | Forthcoming (W#79 PR 0 substrate; not yet merged at ADR 0097 R1 authoring time) | partial — W#79 Rev 2 sec-eng A + .NET-arch K1 invariant locks `IPasswordHasher<UserEntity>` ctor injection at the handler tier; ADR 0097 Step 2 swaps composition-root registration without touching handler callsite |

---

## Context

W#79 sub-cohort 1 introduces Sunfish's first authentication-credential-storage
surface: the public signup endpoint family (`POST /api/signup`,
`POST /api/signup/verify-email`, `POST /api/signup/resend-verification`,
`POST /api/signup/check-availability`) runs a `SignupHandler` whose first
load-bearing step (per W#79 Rev 2 sec-eng D constant-work discipline) is
unconditional `IPasswordHasher<UserEntity>.HashPassword(stubUser, request.Password)`
on every request, irrespective of email-availability outcome. The W#79 hand-off
locks `IPasswordHasher<UserEntity>` (typed-generic, ASP.NET Identity contract) as
the ctor-injected callsite per sec-eng A + .NET-arch K1: "interface, NEVER static;
ADR 0097 future Argon2id swap requires zero callsite changes." W#79 ships at MVP
with `Microsoft.AspNetCore.Identity.PasswordHasher<UserEntity>` as the concrete —
`PasswordHasherCompatibilityMode.IdentityV3` = PBKDF2-HMAC-SHA256, 100k iterations,
version byte 0x01.

The W#79 H8 Admiral ruling (`admiral-ruling-2026-05-26T0035Z`) ratified this
MVP-ship configuration **as a deferred-with-commitment**: PBKDF2 V3 100k is
NIST-conformant (SP 800-63B-3 §5.1.1.2 names ≥10k iterations as the
memorized-secret-verifier floor; 100k is 10× the floor) and acceptable for the
MVP window, but **not acceptable for production-customer onboarding**. The
industry consensus on Argon2id for new systems is settled (2017 password-hashing-
competition winner; 2021 IETF RFC 9106 standardization; 2024 OWASP cheatsheet
primary recommendation; 2024 OWASP ASVS Level-1/2/3 inclusion). OWASP's
canonical position retrieved 2026-05-27: **"Argon2id is the default choice for
general-purpose applications; PBKDF2 is acceptable only for FIPS-mandated
environments."** Sunfish has no FIPS-mandate posture (verified — no
FIPS-compliance concern in ADR 0046 production-readiness checklist); PBKDF2 is
not the correct production-tier algorithm. ADR 0097 is the deferred commitment.

ONR's scaffold (`shipyard#167`; 1167 lines) surveyed the status quo
(`grep -rn "IPasswordHasher" packages/ --include="*.cs"` returns zero shipyard
hits; W#79 SignupHandler is the first and only forthcoming consumer), enumerated
six candidate substrate designs (Option A keep-PBKDF2-raise-iter; Option B
bcrypt; Option C Argon2id-via-Konscious-with-interface-retention; Option D
Argon2id-via-Konscious-with-non-generic-IPasswordHasher-wrapper; Option E
Argon2id-via-Konscious-with-full-ADR-0096-Tier-2-discipline; Option F
Argon2id-via-Konscious-with-Tier-1-mock-first-discipline), surfaced 11 halt
conditions for Admiral disposition, and recommended **Option C + Option F at
Sub-option α** (new `packages/foundation-password-hashing/` substrate package;
Argon2id via Konscious; preserve `IPasswordHasher<TUser>` interface; introduce
`IMockPasswordHasher` marker + `MockPasswordHasherProductionGuardAssertion`
IHostedService at Tier-1 boundary; cross-tier reuse of ADR 0096's mock-first
discipline pattern). The Admiral 11-halt ruling
(`admiral-ruling-2026-05-27T1430Z`) RATIFIED all 11 at ONR-recommended defaults
in single pass.

A naive Argon2id substitution introduces three load-bearing risks the ADR must
codify into the substrate itself:

1. **Migration-detection mechanism.** W#79-MVP-era hashes are PBKDF2 V3 byte[]
   format (recognizable by Base64-decode → leading byte 0x01). When ADR 0097
   Step 2 cuts over the composition-root registration to Argon2id, the
   `Argon2idPasswordHasher<TUser>.VerifyHashedPassword(...)` implementation MUST
   recognize both formats: write Argon2id-PHC-string format going forward; verify
   legacy PBKDF2 V3 byte[] format on read; return
   `PasswordVerificationResult.SuccessRehashNeeded` on successful legacy-hash
   verify so the W#80 login handler can rehash and write the canonical Argon2id
   format back to storage. Lazy rehash-on-next-login is the only viable
   migration path — bulk rehash is **physically impossible** without retrievable
   plaintext (passwords are not stored in plaintext; the hash is a one-way
   function by construction).

2. **Mock-leak-to-production foot-gun.** Tier-1 domain-blocks normally do not
   ship mocks (the concrete is the only registration; no swap surface). But
   the W#79 sec-eng D constant-work discipline already absorbs the
   tens-of-milliseconds Argon2id cost unconditionally per request; unit tests
   that exercise the SignupHandler benefit substantially from a fast-test-loop
   `MockPasswordHasher<TUser>` that returns a deterministic no-op hash. Once a
   mock impl exists in the substrate, the same silent-bypass risk that motivated
   ADR 0096's `IMockVendorProvider` + `MockProviderProductionGuardAssertion`
   applies at Tier-1: a composition-root mis-wiring that registers the mock
   instead of the Argon2id-backed concrete in production would silently bypass
   password hashing. ADR 0097 codifies a Tier-1 analog of the ADR 0096 pattern:
   `IMockPasswordHasher` marker on `MockPasswordHasher<TUser>`;
   `MockPasswordHasherProductionGuardAssertion : IHostedService` that runs a
   `ServiceDescriptor` registration-snapshot scan at `StartAsync` and throws
   `MockPasswordHasherInProductionException` when production env + mock concrete
   + no opt-out env-var. The `IMockPasswordHasher` interface is a **distinct
   marker family** from `IMockVendorProvider`: conflating them would assert that
   Argon2id is a vendor surface, which it is not (Argon2id has one canonical
   algorithm per RFC 9106 and one canonical .NET library per OWASP cheatsheet;
   parameter hardening is the only "swap" axis and is not vendor-shaped).

3. **Parameter-hardening upgrade-without-invalidation.** OWASP's parameter floor
   may rise over time (m=19 MiB → m=46 MiB → m=64 MiB historical progression).
   Existing hashes computed at the older floor MUST remain verifiable; the
   substrate-tier upgrade mechanism is `VerifyHashedPassword` returning
   `SuccessRehashNeeded` when the embedded `m=` / `t=` / `p=` parameters in the
   stored PHC string are below the current `Argon2idHashOptions` floor. The
   Konscious-canonical PHC string format encodes the parameters in the wire
   format itself — the verify path inspects them via string parsing without
   auxiliary metadata storage. This is the load-bearing wire-format property
   that makes parameter-floor hardening a deployment-tier operation rather than
   a forced-password-reset event.

This ADR codifies **one canonical Tier-1 PasswordHasher Substrate** that
addresses all three risks at substrate-tier rather than guidance-tier:

1. `Sunfish.Foundation.PasswordHashing.Argon2idPasswordHasher<TUser>` implements
   `Microsoft.AspNetCore.Identity.IPasswordHasher<TUser>` via Konscious.Argon2id
   primitive; writes PHC string format; verifies both PHC string and legacy V3
   byte[] formats with `SuccessRehashNeeded` on legacy-success and on
   below-floor-parameter-success.
2. `Argon2idHashOptions` carries `(MemoryKib, Iterations, DegreeOfParallelism)`
   triple bound via `IOptions<Argon2idHashOptions>` singleton snapshot; pinned
   at OWASP minimum (19456 / 2 / 1) by default; tunable per deployment.
3. `MockPasswordHasher<TUser> : IPasswordHasher<TUser>, IMockPasswordHasher` ships
   in the same substrate package; `MockPasswordHasherProductionGuardAssertion :
   IHostedService` scans the registration tree at `StartAsync` and fails loudly
   when production + mock-without-opt-out.

The substrate is **Tier-1 domain-block** in the slotting-architecture taxonomy
(shipyard#152): concrete DI; no vendor swap; the algorithm is fixed at
substrate-tier ratification, not at deployment time. Cross-tier reuse of the
ADR 0096 mock-first + production-guard pattern is the cheapest way to absorb the
Tier-2-proven discipline at the Tier-1 boundary; the `IMockPasswordHasher`
marker family is distinct from `IMockVendorProvider` to preserve the
"PasswordHasher is not a vendor" semantic accuracy. The first consumer — the
W#79 sub-cohort 1 `SignupHandler` — already injects `IPasswordHasher<UserEntity>`
per the W#79 Rev 2 sec-eng A invariant; the W#79 Step 2 composition-root cutover
is a single-line DI substitution with **zero handler-tier callsite changes**.
The W#80 login-handler hand-off (downstream sub-cohort) consumes the
`SuccessRehashNeeded` migration trigger and writes the rehashed Argon2id-format
hash back to `User.PasswordHash` via `IUserRepository.UpdatePasswordHashAsync`.

This ADR is **substrate-tier and pre-production-mandatory**: not MVP-blocking
(W#79 ships with PBKDF2 V3 default per the H8 deferred-with-commitment ruling),
but **production-launch-blocking** — Steps 1-2 MUST merge before W#79 reaches
production-customer onboarding.

## Decision drivers

**D1 — Argon2id is the canonical algorithm; the substrate is the only meaningful
abstraction.** OWASP cheatsheet, RFC 9106, OWASP ASVS, and ASP.NET Core Identity
roadmap all converge on Argon2id as the primary recommendation for new systems.
The .NET ecosystem has a single canonical implementation
(`Konscious.Security.Cryptography.Argon2` v1.3.1; ~700k NuGet downloads;
OWASP-cheatsheet-recommended; MIT-licensed; Argon2-1.3-spec-conformant). There
is **no vendor surface** the way Postmark vs SendGrid is a vendor surface for
`IEmailProvider` — Argon2id has one canonical primitive. The substrate-tier
abstraction is therefore concrete (Tier-1 domain-block discipline; ADR 0092
sibling): one interface (`IPasswordHasher<TUser>`, BCL), one concrete
(`Argon2idPasswordHasher<TUser>`, Sunfish-owned), one mock concrete
(`MockPasswordHasher<TUser>`, dev-only). The slotting-architecture taxonomy
(the shipyard slotting-architecture survey) names this exactly: Tier-1 substrate
is concrete DI with no vendor swap; ADR 0097 does not introduce a vendor swap
axis.

**D2 — Preserve `IPasswordHasher<TUser>` callsite invariant per W#79 sec-eng A.**
The W#79 Rev 2 fold pinned `IPasswordHasher<UserEntity>` (typed-generic, ASP.NET
Identity contract) as the SignupHandler injection point with the explicit
rationale: *"interface, NEVER static; ADR 0097 future Argon2id swap requires
zero callsite changes."* This is load-bearing for the substrate's value
proposition — the swap is a composition-root one-liner (`services.AddSingleton<
IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>()` →
`services.AddSunfishPasswordHashing<UserEntity>(...)`), not a handler-tier
refactor. The decision to retain the typed-generic interface (rather than
introduce a Sunfish-owned non-generic `IPasswordHasher` wrapper per scaffold
Option D) is RATIFIED at this driver — speculative decoupling from
`Microsoft.AspNetCore.Identity.Core` is not a substrate-tier concern; the
existing typed interface is sufficient.

**D3 — Migration-detection via PHC-string format + legacy-V3 byte[] fallback.**
The substrate-tier wire format is the Konscious-canonical Argon2id PHC string:
`$argon2id$v=19$m=19456,t=2,p=1$<base64-salt>$<base64-hash>`. This is the
industry-standard format (referenced by RFC 9106; produced and consumed by every
modern Argon2 library across languages; OWASP-cheatsheet-canonical). Three
properties make it load-bearing: (a) **self-describing** — the wire format
encodes the algorithm + version + parameters, so the verify path needs no
auxiliary metadata; (b) **future-tunable** — when the substrate-tier
`Argon2idHashOptions` floor rises (m=19456 → m=46080 → m=65536), the verify
path detects below-floor stored hashes by inspecting the embedded parameters and
returns `SuccessRehashNeeded` automatically; (c) **discriminable from legacy V3**
— ASP.NET Identity V3 byte[] format Base64-decodes to a leading 0x01 version
byte, while Argon2id PHC strings begin with the literal `$argon2id$` prefix,
making the read-side discrimination trivial. The Argon2id concrete delegates
legacy-V3 verify to a privately-composed
`Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` instance (the BCL default)
and returns `SuccessRehashNeeded` on legacy-success — the migration trigger that
W#80's login handler observes to rehash and write the canonical format back.

**D4 — Production-safety substrate via Tier-1 mock-first + production-guard.**
Tier-1 domain-blocks normally do not ship mocks (the concrete is the only
registration; no swap surface). PasswordHasher is the exception: the
fast-test-loop value of a no-op `MockPasswordHasher<TUser>` (returns
`"mock-hash-of-len-{N}"` deterministically; verify returns `Success` iff the
candidate length matches the stored mock-hash's length) is substantial — every
SignupHandler unit test would otherwise pay Argon2id's tens-of-milliseconds cost
per invocation, accumulating to multi-second test-suite penalty. Shipping the
mock requires the production-safety invariant from day one to avoid the
mock-leak-to-production foot-gun. ADR 0097 codifies three coordinated invariants
(direct stylistic and mechanistic analog of ADR 0096 D1):

- **D4a (marker interface):** `MockPasswordHasher<TUser>` carries
  `IMockPasswordHasher` as a marker interface in addition to
  `IPasswordHasher<TUser>`. The `IMockPasswordHasher` marker is a **distinct
  marker family** from ADR 0096's `IMockVendorProvider` — conflating them would
  assert that PasswordHasher is a vendor surface (false). Cross-tier reuse of
  the discipline does NOT require cross-tier reuse of the marker interface.
- **D4b (opt-out env var):** Production deployments that intentionally ship with
  the mock (load-test environments, closed demo deployments) must set
  `SUNFISH_ALLOW_MOCK_PASSWORD_HASHER=true` explicitly. The env-var name is
  deliberately alarming + scope-specific; presence-of-truthy-value (per
  `bool.TryParse` canonical BCL semantics; same fail-closed discipline as
  ADR 0096's `SUNFISH_ALLOW_MOCK_PROVIDERS`) is the opt-in semantics.
- **D4c (production-default invariant):** When `ASPNETCORE_ENVIRONMENT=Production`,
  the `MockPasswordHasherProductionGuardAssertion` IHostedService verifies at
  startup that no registered `IPasswordHasher<>` concrete (any `TUser`)
  implements `IMockPasswordHasher`, OR that
  `SUNFISH_ALLOW_MOCK_PASSWORD_HASHER` parses to `true`. Otherwise startup
  fails with `MockPasswordHasherInProductionException` naming the offending
  service type. The invariant is **provable at `StartAsync`** via static
  inspection of `ServiceDescriptor`s — no service resolution occurs in the
  assertion (per ADR 0091 R2 A1 / ADR 0095 R2 A3 / ADR 0096 R2 precedent:
  assertions inspect the registration tree, not the runtime resolution tree).

Together these three invariants make the Tier-1 mock-first discipline
**production-safe by construction**: a composition-root mis-wiring fails at
startup, not at first signup request. The assertion is independent of
ADR 0095's `BootstrapAndTenantMutualExclusionAssertion` and ADR 0096's
`MockProviderProductionGuardAssertion` (different concerns; disjoint
composition-root properties — see §"Substrate / layering notes" on startup
ordering).

**D5 — Tier-1 substrate package geometry: new `foundation-password-hashing/`,
not co-located.** Sub-option β (co-locate in `packages/foundation-authorization/`)
was REJECTED at the Admiral ruling because `foundation-authorization`'s charter
per ADR 0091 R2 is authorization-context resolution — tenant + caller + policy
— while PasswordHasher is authentication-credential-storage, conceptually
upstream of authorization. Sub-option γ (co-locate in
`packages/foundation-integrations/`) was REJECTED because cryptographic
primitives are not integrations in the external-system-bridge sense ADR 0013
codifies. The new `packages/foundation-password-hashing/` package follows the
ADR 0095 / ADR 0096 / ADR 0098 precedent: substrate primitives earn their own
package home. The package is small (~6 types + tests) but establishes the
clean cluster home for future password-policy primitives (HIBP integration;
password-strength meter) if they ever surface.

**D6 — Parameters pinned at OWASP minimum; tunable per deployment; future
hardening via parameter-floor upgrade.** The substrate-tier default is OWASP
minimum (m=19456 KiB = 19 MiB / t=2 / p=1) — ~50-100 ms wall-clock on modern
x86; calibrated to fit within the W#79 sec-eng D ±25% p50/p95 latency parity
floor. Higher-defense parameters (m=46080 / t=1 / p=1 alternative;
m=65536 / t=3 / p=1 industry-hardened; m=131072 / t=3 / p=1 maximum-defensive)
are tunable via `Argon2idHashOptions` binding — deployments may override per
`IConfiguration["Sunfish:PasswordHashing:Argon2id:MemoryKib"]` etc. without
substrate changes. Future substrate-tier hardening (raising the default floor
from 19456 to 46080 or 65536) ships as an ADR amendment + a coordinated
deployment-tier configuration update; existing hashes computed at the old floor
remain verifiable AND are auto-upgraded via `SuccessRehashNeeded` on next login
(per D3's self-describing wire-format property).

**D7 — `IOptions<Argon2idHashOptions>` singleton snapshot, not `IOptionsMonitor`.**
The substrate-tier parameter triple `(MemoryKib, Iterations, DegreeOfParallelism)`
is captured at composition-root build time and immutable for the process
lifetime. Runtime config reload is REJECTED per Halt 11 — substrate-tier
parameter changes are deployment-tier events (require ADR-tier ratification,
which is by definition a deployment-tier event), and a mid-running parameter
change would create a window where hashes-in-flight use different parameters
than newly-issued ones, complicating the `SuccessRehashNeeded` discrimination
on verify. The simpler `IOptions<T>` shape matches the immutability discipline.

## Considered options

**Option A — Keep PBKDF2 V3; raise iteration count from 100k to 600k.**
REJECTED. OWASP cheatsheet (retrieved 2026-05-27) names PBKDF2 as "acceptable
only for FIPS-mandated environments"; Sunfish has no FIPS-mandate posture.
PBKDF2 is not memory-hard — modern GPU + ASIC attacks remain economically
feasible at 600k iterations. The W#79 H8 commitment ("Argon2id per OWASP")
forecloses this option.

**Option B — Migrate to bcrypt via `BCrypt.Net-Next`.** REJECTED. bcrypt is
OWASP's "fallback when Argon2id unavailable" tier; Argon2id IS available via
Konscious. bcrypt has a 72-byte password truncation behavior (depending on
implementation); Sunfish's "≥12 chars" minimum has no cap-related concern in
practice, but the discipline of "use the recommended primary algorithm"
matters at substrate tier. Memory-hard property is weaker than Argon2id's
tunable memory parameter.

**Option C — Argon2id via Konscious + retain `IPasswordHasher<TUser>` interface
+ version-tagged hash output + Tier-1 domain-block placement (RECOMMENDED;
APPROVED).** Adds `Konscious.Security.Cryptography.Argon2` NuGet reference;
implements `Argon2idPasswordHasher<TUser> : IPasswordHasher<TUser>` writing PHC
string format + verifying both PHC string + legacy V3 byte[] format; DI
registration via `AddSunfishPasswordHashing<TUser>` extension; preserves the
W#79 sec-eng A invariant by construction (zero handler-tier callsite changes).
Self-describing PHC wire format supports parameter-floor upgrades via
`SuccessRehashNeeded`. **APPROVED — this option.**

**Option D — Argon2id via Konscious + introduce new non-generic `IPasswordHasher`
wrapper.** REJECTED per D2. Violates W#79 sec-eng A invariant; speculative
future-proofing for "if Sunfish ever drops ASP.NET Identity entirely" — ASP.NET
Identity is Sunfish's identity substrate per ADR 0091 R2's `ICurrentUser`
integration + W#79's `UserEntity : IdentityUser<Guid>`.

**Option E — Argon2id via Konscious + apply ADR 0096 Tier-2 vendor-substrate
discipline.** REJECTED per D1 + D4a. Argon2id is NOT a vendor surface;
Tier-2 vendor-substrate pattern presumes a swap surface that doesn't exist for
cryptographic primitives. The `IMockVendorEnvVarRegistry` mapping presumes the
real adapter is gated on a vendor-API-key env var — Argon2id parameters are
not "credentials" in the same sense. Cross-tier reuse of the mock-first
discipline at Tier-1 (Option F) is the cleaner move.

**Option F — Argon2id via Konscious + apply Tier-1-flavored mock-first
discipline (cheaper Option E; combined with Option C).** APPROVED per Halt 4.
Distinct `IMockPasswordHasher` marker family avoids the Tier-2 vendor-surface
framing while preserving the production-safety property. Adds ~80-100 LOC for
the marker + `MockPasswordHasherProductionGuardAssertion` IHostedService;
acceptable cost for permanent foot-gun closure.

## Decision

**One canonical Tier-1 PasswordHasher Substrate ADR codifying Argon2id-via-
Konscious with `IPasswordHasher<TUser>` interface retention + Tier-1 mock-first
discipline + version-tagged hash output for lazy rehash-on-next-login migration
(Option C + Option F at Sub-option α):**

1. **Argon2id parameters pinned at OWASP minimum** (Halt 1 RATIFY).
   `(MemoryKib, Iterations, DegreeOfParallelism) = (19456, 2, 1)`. ~50-100 ms
   wall-clock on modern x86; matches the W#79 sec-eng D constant-work calibration;
   matches the OWASP cheatsheet primary recommendation. Tunable per deployment
   via `Argon2idHashOptions` binding; future substrate-tier hardening ships as
   ADR amendment.

2. **NuGet library: `Konscious.Security.Cryptography.Argon2` v1.3.1** (Halt 2
   RATIFY). OWASP-cheatsheet-recommended .NET Argon2 library; MIT-licensed;
   ~700k NuGet downloads; Argon2-1.3-spec-conformant; no transitive runtime
   dependencies beyond BCL. Supply-chain risk mitigated by adoption surface +
   OWASP endorsement + dotnet/nuget verification.

3. **New `packages/foundation-password-hashing/` substrate package** (Halt 3
   RATIFY). Sub-option α — substrate-tier own package per the ADR 0095 /
   ADR 0096 / ADR 0098 precedent. Avoids the cluster-confusion cost of
   co-locating with `foundation-authorization` (Sub-option β; rejected for
   authn-vs-authz semantic distinction) and avoids the vendor-substrate framing
   of co-locating with `foundation-integrations` (Sub-option γ; rejected per D1).

4. **Mock-first discipline applied at Tier-1 boundary** (Halt 4 RATIFY).
   Cross-tier reuse of ADR 0096's pattern, NOT cross-tier reuse of ADR 0096's
   `IMockVendorProvider` marker. New distinct `IMockPasswordHasher` marker
   family + `MockPasswordHasherProductionGuardAssertion` IHostedService at
   substrate-tier. The mock-without-marker compile-time foot-gun is closed via
   a generic constraint on `AddSunfishMockPasswordHashing<TUser>` — only types
   implementing both `IPasswordHasher<TUser>` and `IMockPasswordHasher` can
   register via this helper (see §Implementation roadmap Step 1).

5. **Migration: lazy rehash-on-next-login only** (Halt 5 RATIFY). Bulk-rehash
   is **physically impossible** (no plaintext retrievable); lazy is the only
   safe path. Pre-MVP cohort size is zero (no real users yet);
   MVP-launch-to-production-customer-onboarding window is small (weeks-to-months
   per CIC sequencing). `Argon2idPasswordHasher.VerifyHashedPassword` returns
   `SuccessRehashNeeded` on legacy-V3-success and on below-floor-parameter-success;
   the W#80 login handler observes the trigger and rehashes via
   `HashPassword(...)` + persists via `IUserRepository.UpdatePasswordHashAsync`.

6. **Hash output format: Konscious-canonical Argon2id PHC string** (Halt 6
   RATIFY). `$argon2id$v=19$m=19456,t=2,p=1$<base64-salt>$<base64-hash>`.
   Industry-standard self-describing format; parameter-versioned for future
   migration; instantly recognizable by ops + sec tooling; parsable by any
   Argon2id-aware tool across languages.

7. **No pepper at MVP; nullable field reserved for future** (Halt 7 RATIFY).
   `Argon2idHashOptions.Pepper` exists as `byte[]?` field with default null
   (= disabled). OWASP cheatsheet treats pepper as **optional**
   defense-in-depth, not a baseline requirement. Pepper requires secret-store
   substrate (rotation + audit + compromise semantics); deferred pending future
   secret-store substrate ADR. Future enable is a config change, not an
   algorithm change.

8. **Pre-production sequencing: Steps 1-2 merge before W#79 reaches
   production-customer onboarding** (Halt 8 RATIFY). Per the W#79 H8 commitment:
   "ADR 0097 MUST land before W#79 reaches production." Step 1 (substrate
   package) + Step 2 (W#79 SignupHandler composition-root cutover) both gate
   production launch; MVP launch acceptable with PBKDF2 V3 default per H8
   deferred-with-commitment.

9. **Dual-council MANDATORY on ADR text + Step 1 PR; sec-eng MANDATORY
   SPOT-CHECK on Step 2 PR** (Halt 9 RATIFY). Per H8 commitment + substrate-tier
   cryptography discipline. .NET-architect council reviews the substrate
   geometry + DI helper shape + IHostedService assertion mechanism; sec-eng
   council reviews the cryptographic-primitive correctness + production-safety
   invariants + migration-trigger semantics + parameter selection rationale.
   Step 1 PR re-pulls both councils on the implementation surface; Step 2 PR
   pulls sec-eng on the composition-root cutover correctness (one-line
   substitution; the substitution invariant is mechanically verifiable but
   sec-eng SPOT-CHECK confirms no inadvertent invariant violation).

10. **Backward-compat: legacy V3 PBKDF2 verify + `SuccessRehashNeeded` on
    success** (Halt 10 RATIFY). `Argon2idPasswordHasher.VerifyHashedPassword`
    composes a private `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>`
    instance for legacy-format fallback verify; on success returns
    `PasswordVerificationResult.SuccessRehashNeeded`; on failure returns
    `PasswordVerificationResult.Failed`. The fallback is currently a safety net
    (W#79 MVP-era hashes are the only pre-Argon2id state and the cohort is
    expected to be small); the discipline is permanent (future legacy formats
    would extend the same dispatch pattern).

11. **`IOptions<Argon2idHashOptions>` singleton snapshot, not `IOptionsMonitor`**
    (Halt 11 RATIFY). Parameters fixed at composition-root build time;
    substrate-tier parameter changes ship via deployment; no runtime reload.
    `IOptionsSnapshot<T>` REJECTED — singleton scope cannot depend on
    per-request scope (service-locator anti-pattern).

## Substrate / layering notes

**Tier-1 domain-block placement.** PasswordHasher is a Tier-1 substrate per the
slotting-architecture taxonomy: concrete DI; no vendor swap; the algorithm is
fixed at substrate-tier ratification. Tier-1 substrate normally does not ship
mocks (the concrete is the only registration), but ADR 0097 adopts cross-tier
reuse of ADR 0096's mock-first discipline at the Tier-1 boundary for the
fast-test-loop value + permanent foot-gun closure (D4). The
`IMockPasswordHasher` marker is a **distinct marker family** from
`IMockVendorProvider`; the substrate gains a "mock-marker family" rather than a
single canonical `IMockProvider` marker. Halt 4 ratifies this duplication as
the correct trade — conflating the markers would assert that PasswordHasher is
a vendor surface (false; D1).

**Interaction with ADR 0013 (Foundation.Integrations).** ADR 0013 codifies
provider-neutrality discipline for external vendor surfaces (HttpClient-only
adapters; vendor SDK exclusion; `IProviderRegistry` + `ProviderDescriptor`
registration). ADR 0097 does NOT adopt the ADR 0013 discipline — Argon2id is
not a network vendor surface; the Konscious NuGet is a cryptographic primitive
library, not a vendor SDK in the ADR 0013 sense. The ADR 0013 spirit
("substrate-tier minimum-floor; no implicit vendor lock-in") applies; the
ADR 0013 letter (HttpClient-only; no SDK) does not.

**Interaction with ADR 0091 R2 (ITenantContext Divergence).** The
`MockPasswordHasherProductionGuardAssertion` IHostedService is a direct
stylistic and mechanistic analog of ADR 0091 R2's
`TenantContextRegistrationAssertion` (also IHostedService; also "fail loudly at
startup"; also closes a silent-DI-mis-wiring foot-gun via static
`ServiceDescriptor` inspection). The two assertions are independent
(different concerns: one tenant-context coherence, one mock-password-hasher
production-safety) on disjoint composition-root properties; both adopt the
"POSITIVE invariant provable at startup via static descriptor inspection"
pattern per ADR 0091 R2 A1.

**Interaction with ADR 0095 (Bootstrap Context).** ADR 0095 establishes the
`IBootstrapContext` substrate for pre-tenant signup. The W#79 SignupHandler runs
in bootstrap scope and consumes `IPasswordHasher<UserEntity>` as a ctor
dependency; the PasswordHasher is **bootstrap-context-agnostic** (the hash
computation does not depend on tenant identity — passwords are user-keyed, not
tenant-keyed). ADR 0097 substrate registration belongs in the same
composition-root scope as ADR 0095 (the signal-bridge / onboarding cluster) but
the substrate itself has no `IBootstrapContext` or `ITenantContext` dependency.

**Interaction with ADR 0096 (Tier-2 Vendor-Provider Substrate).** ADR 0097
inherits ADR 0096's **mock-first + production-guard discipline pattern** at the
substrate-tier shape level but introduces a **distinct marker family**
(`IMockPasswordHasher` vs `IMockVendorProvider`) at the interface level. The
two production-guard IHostedServices (ADR 0096's
`MockProviderProductionGuardAssertion` + ADR 0097's
`MockPasswordHasherProductionGuardAssertion`) are **independent on disjoint
composition-root properties** — ADR 0096's assertion scans for
`IMockVendorProvider`-marker implementations of Tier-2 contracts (email,
CAPTCHA, etc.); ADR 0097's assertion scans for `IMockPasswordHasher`-marker
implementations of `IPasswordHasher<>`. Neither assertion resolves services the
other registers; ordering between them does not affect correctness. The
canonical registration ordering (per ADR 0096 §"Substrate / layering notes")
places ADR 0095's `BootstrapAndTenantMutualExclusionAssertion` first (composition-
root coherence), then ADR 0096's `MockProviderProductionGuardAssertion` (Tier-2
production-safety), then ADR 0097's `MockPasswordHasherProductionGuardAssertion`
(Tier-1 cryptographic-primitive production-safety) — same disjoint-invariant
discipline; ordering does not change correctness but the canonical chain is
helpful for operator-debugging coherence.

**Interaction with ADR 0098 (Block-Naming Generalization).** ADR 0098's Tier-1
domain-block discipline applies to vertical blocks (`blocks-leases`,
`blocks-rent-collection`, etc.); ADR 0097's substrate is a foundation-tier
primitive (`foundation-password-hashing`), structurally sibling to
`foundation-authorization` and `foundation-bootstrap`. ADR 0098 does not touch
the foundation-tier; ADR 0097 does not touch vertical blocks. The two ADRs share
cadence (substrate-tier Rev 1 → dual-council → Rev 2 if AMBER → re-attest) but
not surface area.

**Cross-tier mock-marker family discipline.** ADR 0097 establishes the pattern
that **mock-marker interfaces are substrate-scoped, not fleet-scoped**. Future
Tier-1 substrates that adopt the mock-first discipline (e.g., a hypothetical
`foundation-time` substrate with `MockClock`) would introduce their own
`IMockClock` marker family; future Tier-3 capability-plugin substrates (e.g.,
flight-deck TTS/STT) would introduce their own marker families. The marker is
the substrate's positive identification mechanism; sharing markers across
substrates would conflate "this substrate's mock" semantics. The discipline IS
the pattern; the marker is the substrate's instance of the pattern. Forward
ADR amendments to substrate-tier ADRs that adopt mock-first should follow this
discipline.

**Supply-chain posture (S4 sec-eng substrate amendment).** Three substrate-
tier supply-chain floors govern the Konscious dependency:

- **Konscious maintenance posture.** `Konscious.Security.Cryptography.Argon2`
  v1.3.1 dates to late 2022; the maintenance posture is "stable, spec-
  conformant, not actively developed" rather than "active maintenance." This
  is acceptable for cryptographic primitive libraries that conform to a
  stable spec (RFC 9106 §5 reference vectors are immutable; spec-drift is
  not a concern for the algorithm itself). The substrate's Revisit Trigger
  #5 (`Konscious deprecated, archived, or security advisory`) governs the
  replacement decision.
- **RFC 9106 reference-vector parity floor.** Step 1 PR unit-test surface
  MUST include at least 3 RFC 9106 §5 reference-vector parity tests pinned
  to the published test vectors (input password + salt + parameters →
  expected hash output). Without these, a future Konscious bump that
  silently drifts from spec conformance would not be caught at substrate
  tier — a real bug-class that has shipped in cryptographic libraries
  before. The reference vectors are immutable spec-tier discipline.
- **NuGet package integrity floor.** Step 1 csproj MUST commit a
  `packages.lock.json` AND enable `<RestoreLockedMode>true</RestoreLockedMode>`.
  Substrate-tier cryptographic-primitive dependency bumps require explicit
  ADR amendment, not silent `dotnet restore` regeneration. This floor
  codifies the supply-chain discipline that fleet-conventions reference
  template (`foundation-authorization` NU1510 suppression) is silent on —
  ADR 0097 IS the substrate that establishes this floor for cryptographic-
  primitive substrate dependencies. Forward-watch: future Tier-1
  cryptographic-primitive substrate ADRs inherit this floor.

## Cryptographic floor requirements

The substrate codifies the following minimum-floors as load-bearing
substrate-tier invariants. Step 1 sec-eng SPOT-CHECK confirms these; Step 2
PR adoption assumes them. Each floor is **non-substitutable downward** —
implementations MAY tighten (higher m, higher t) but MUST NOT loosen.
**Floors 3, 4, 5 (and the salt/hash-length floors 1, 2) are enforced at
host startup via `Argon2idParameterFloorAssertion : IHostedService`** (S3
sec-eng substrate amendment; see §"Implementation roadmap Step 1"); the
non-substitutable-downward property is substrate-tier-enforced, not
documentation discipline. The validator
`Argon2idHashOptionsValidator : IValidateOptions<Argon2idHashOptions>` is
the defense-in-depth companion at the `IOptions<T>` validation layer (C3
clarification).

1. **Salt length floor: 16 bytes minimum** (`Argon2idHashOptions.SaltLengthBytes`
   default 16). Generated via
   `System.Security.Cryptography.RandomNumberGenerator.Fill(saltSpan)`
   (Span-typed, allocation-free per .NET-canonical post-net5; C8
   clarification) — cryptographically random. NO custom RNG; NO process-shared
   seeded RNG; the BCL `RandomNumberGenerator` is the only acceptable source
   per OWASP cheatsheet + RFC 9106.

2. **Hash output length floor: 32 bytes minimum** (`Argon2idHashOptions.HashLengthBytes`
   default 32). Matches Argon2id-1.3 default; encodes to ~43 base64 chars in
   the PHC string.

3. **Memory floor: m ≥ 19456 KiB (= 19 MiB)** (`Argon2idHashOptions.MemoryKib`
   default 19456). OWASP cheatsheet minimum; do not lower. Raise per deployment
   if hardware supports + signup-latency budget tolerates.

4. **Iteration floor: t ≥ 2** (`Argon2idHashOptions.Iterations` default 2). OWASP
   cheatsheet minimum at the m=19 MiB tier. Lower iteration counts are
   acceptable only at the m=46 MiB alternative tier (t=1; not the substrate
   default).

5. **Parallelism: p = 1** (`Argon2idHashOptions.DegreeOfParallelism` default 1).
   Higher parallelism does not improve security but consumes more CPU per
   request; substrate default p=1 matches OWASP cheatsheet.

6. **No password truncation + input-length bounding (S6 sec-eng substrate
   amendment).** Unlike bcrypt's 72-byte truncation behavior, Argon2id has
   no length cap; the substrate MUST NOT truncate, hash, or pre-digest the
   input password before passing to Konscious's `Argon2id` constructor.
   `Encoding.UTF8.GetBytes(password)` is the canonical conversion path
   (matches Konscious's expected input shape; matches OWASP cheatsheet's
   "treat password as UTF-8 bytes" guidance). **Substrate-tier defense-in-
   depth input-length floors (S6):** `VerifyHashedPassword` MUST short-
   circuit-`Failed` when `hashedPassword.Length > 1024` (PHC strings are well
   under 200 chars; 1024 is a generous ceiling that prevents the verify path
   from becoming a DoS surface for malicious long-input callers).
   `HashPassword` MUST throw `ArgumentException` and `VerifyHashedPassword`
   MUST return `Failed` when `providedPassword.Length > 4096` (4 KiB is
   generously above the W#79 ≥12-char minimum + plausible passphrase
   ceiling; complements the W#79 handler-tier input-length cap with
   substrate-tier defense). `Argon2idHashOptions.Pepper.Length` MUST be
   bounded ≤ 64 bytes when bound at composition-root (forward-watch floor
   for post-secret-store-substrate-ADR future enablement; the
   `Argon2idParameterFloorAssertion` per S3 asserts this at
   `StartAsync`).

7. **No-log-password discipline.** Plaintext passwords MUST NOT appear in log
   messages, exception messages, or trace events emitted from the substrate.
   Adapter exceptions wrapping verification failures MUST scrub any
   password-derived material before re-throwing. **Floor 7 corollary
   (S2 sec-eng substrate amendment):** the `MockPasswordHasher<TUser>`
   stored hash format embeds NO password-derived material — the
   constant-string format `"mock-hash"` carries zero side-channel-leakage
   from the mock. The substrate-tier discipline is zero-side-channel-leakage
   from the mock, not "minimal-side-channel-leakage."

8. **Constant-work / timing-attack mitigation discipline + Argon2id-
   specifically primitive selection (S5 sec-eng substrate amendment).** The
   Argon2id primitive itself provides timing-attack resistance for the hash
   computation (Argon2id is the side-channel-resistant variant per RFC 9106;
   data-independent memory access pattern for the first half of the
   computation, data-dependent for the second half). The substrate inherits
   this — no additional timing-equalization is required at the substrate
   tier. The W#79 sec-eng D constant-work discipline at the SignupHandler
   tier is a separate concern (the handler unconditionally invokes
   `HashPassword` even when email is taken; ADR 0097 substrate does not
   participate in this discipline beyond providing the primitive).
   **Argon2id-specifically primitive selection assertion (S5
   LOAD-BEARING).** The Konscious library exposes three Argon2 variants —
   `Argon2i` (data-independent; side-channel-resistant; GPU-non-resistant),
   `Argon2d` (data-dependent; GPU-resistant; side-channel-vulnerable),
   `Argon2id` (hybrid; OWASP-recommended for password storage). A typo in
   the substrate code that instantiated the wrong variant would NOT be
   caught by the PHC string format (the format string `$argon2id$v=19$...`
   is hardcoded; the wire format does NOT cross-verify the primitive).
   Step 1 unit-test surface MUST include a primitive-selection assertion
   test: instantiate `Argon2idPasswordHasher<TUser>`; invoke `HashPassword`
   against a known seed; reflectively-or-by-substrate-assertion verify the
   underlying Argon2 variant invoked is
   `Konscious.Security.Cryptography.Argon2id` specifically (not `Argon2i`
   or `Argon2d`). Without this floor, the Argon2id-specifically property is
   documentation discipline rather than substrate-tier-enforced;
   primitive-selection drift is the load-bearing risk.

9. **Pepper application via Argon2 `KnownSecret` (S1 sec-eng substrate
   amendment).** Pepper application MUST use Konscious's
   `Argon2id.KnownSecret` property (RFC 9106 §3.1 `K` parameter — Argon2's
   canonical secret-value surface that mixes the secret into the hash
   construction in a cryptographically well-defined manner); MUST NOT XOR
   the pepper into the password bytes (undefined semantics for non-
   overlapping ranges; the algorithm natively supports the secret-value
   surface); MUST NOT HMAC-key the pepper-over-password before passing to
   Argon2id (the in-primitive application is the canonical OWASP-cheatsheet
   path for an Argon2-aware implementation). The XOR-into-password-bytes
   pattern is REJECTED as non-canonical — see Q3 RESOLVED disposition.

These nine floors are written assuming Argon2id as the canonical algorithm;
future substrate-tier hardening (raising the m floor per OWASP guidance
update) would amend Floor 3 specifically without touching the other eight.

## Implementation roadmap

Two Step PRs gate production launch. Step 1 is the substrate-package shipment
(the load-bearing surface); Step 2 is the W#79 SignupHandler composition-root
cutover (the consumer integration). An optional Step 3 (background bulk-rehash
job) is enumerated for completeness but is REJECTED per the Halt 5 disposition
(physically impossible without retrievable plaintext).

### Step 1 — `foundation-password-hashing` substrate package PR

Branch shape: `feat/adr-0097-step-1-foundation-password-hashing` (Engineer-
authored post-ADR Acceptance).

Scope:

- New package `packages/foundation-password-hashing/`:
  - `Sunfish.Foundation.PasswordHashing.csproj` — **TFM: `net11.0` per fleet
    `Directory.Build.props` convention** (A1 LOAD-BEARING). The csproj does NOT
    declare an explicit `<TargetFramework>` element — it inherits the
    unconditional `<TargetFramework>net11.0</TargetFramework>` from
    `shipyard/Directory.Build.props` line 5. Engineer does NOT pick at authoring
    time; analyzer-style `netstandard2.0` targeting is the documented exception
    class (Roslyn analyzers only — `packages/foundation-wayfinder-analyzers/`,
    `packages/foundation-ships-office.analyzers/`) and does NOT apply to this
    substrate. The fleet preview.4 era pins every `Microsoft.Extensions.*`
    family member to `11.0.0-preview.4.26230.115` in
    `Directory.Packages.props`; dropping below `net11.0` would lose access to
    these versions.
  - **CPM addition required (A2 LOAD-BEARING).**
    `Microsoft.AspNetCore.Identity.Core` is NOT currently in
    `shipyard/Directory.Packages.props` (verified zero matches for
    "Microsoft.AspNetCore.Identity" / "Identity.Core"). Step 1 PR adds the
    `<PackageVersion Include="Microsoft.AspNetCore.Identity.Core" Version="11.0.0-preview.4.26230.115" />`
    entry pinned to the preview.4 cohort band (matching the rest of the
    `Microsoft.AspNetCore.*` family). The CPM-prop change is **fleet-scope**;
    the substrate csproj then declares `<PackageReference Include="Microsoft.AspNetCore.Identity.Core" />`
    without an inline Version. Without this prerequisite, the first
    substrate-package restore fails with NU1604 ("no version specified").
  - **csproj `<PackageReference>` set — 5 references minimal (Q7 RESOLVED via
    A1 + A2 + clarification):**
    `Microsoft.AspNetCore.Identity.Core` (BCL `IPasswordHasher<TUser>` interface
    + `PasswordVerificationResult` enum + the BCL `PasswordHasher<TUser>` for
    legacy-V3 fallback verify);
    `Konscious.Security.Cryptography.Argon2` (per Halt 2; v1.3.1 band; CPM-pin);
    `Microsoft.Extensions.DependencyInjection.Abstractions` (for
    `ServiceDescriptor` / `IServiceCollection` / `services.Replace` /
    `TryAddEnumerable` — all live in the Abstractions surface on the modern
    BCL); `Microsoft.Extensions.Hosting.Abstractions` (for `IHostedService`);
    `Microsoft.Extensions.Options` (for `IOptions<Argon2idHashOptions>` —
    the substrate consumes `IOptions<T>` only; configuration binding via
    `IConfiguration.GetSection(...)` is a composition-root concern, not a
    substrate-package concern, so
    `Microsoft.Extensions.Options.ConfigurationExtensions` is NOT required).
    **`Microsoft.Extensions.DependencyInjection` (the non-Abstractions
    package) is NOT required** — the substrate package consumes only the
    Abstractions surface. NU1510 NoWarn (`<NoWarn>CS1591;NU1510</NoWarn>`)
    follows the `packages/foundation-authorization/Sunfish.Foundation.Authorization.csproj`
    lines 13-21 precedent per fleet-conventions "NU1510 — keep the PackageRef,
    suppress the warning" (reference template shipyard commit `b4475df`).
  - **Supply-chain pinning floor (S4 sec-eng substrate amendment).** Step 1
    csproj MUST commit a `packages.lock.json` AND set
    `<RestoreLockedMode>true</RestoreLockedMode>`. Substrate-tier
    cryptographic-primitive dependency bumps require explicit ADR amendment,
    not silent `dotnet restore` regeneration. This is the substrate that
    establishes this floor for cryptographic-primitive substrate dependencies;
    forward-watch — future Tier-1 cryptographic-primitive substrate ADRs
    inherit this floor.
  - `Argon2idPasswordHasher.cs` —
    `public sealed class Argon2idPasswordHasher<TUser> : IPasswordHasher<TUser> where TUser : class`.
    Ctor: `(IOptions<Argon2idHashOptions> options)` — captures the options
    snapshot. Composes a private `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>`
    instance constructed via `new PasswordHasher<TUser>(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3 }))`
    for legacy-V3 fallback verify. `HashPassword(TUser user, string password)`:
    enforces the input-length floor first (S6 sec-eng substrate amendment) —
    if `password.Length > 4096` throws `ArgumentException` naming the offending
    parameter (substrate-tier complement to the W#79 handler-tier input-length
    cap); generates 16-byte salt via
    `RandomNumberGenerator.Fill(saltSpan)` (Span-typed, allocation-free per
    .NET-canonical post-net5; C8 clarification); instantiates
    `Konscious.Security.Cryptography.Argon2id(Encoding.UTF8.GetBytes(password))`;
    sets `Salt = salt`, `Iterations = (int)options.Iterations`,
    `MemorySize = (int)options.MemoryKib` (C7 — Konscious `MemorySize` is
    `int`; the `uint` options field is cast at the assignment site; safe for
    all values up to `int.MaxValue`; OWASP-floor 19456 is well within range),
    `DegreeOfParallelism = (int)options.DegreeOfParallelism`;
    if `options.Pepper is not null`, **sets `argon2id.KnownSecret = options.Pepper`**
    per RFC 9106 §3.1 `K` (secret value) parameter — Argon2's canonical
    secret-value surface that mixes the secret into the hash construction in a
    cryptographically well-defined manner (S1 sec-eng substrate amendment;
    `Konscious.Security.Cryptography.Argon2id.KnownSecret` property is the
    load-bearing primitive). The XOR-into-password-bytes pattern is
    REJECTED as non-canonical (undefined semantics for non-overlapping ranges;
    reinvents what the algorithm natively supports — see Floor 9 in
    §"Cryptographic floor requirements"). Pepper is null at MVP per Halt 7;
    future-enablement uses `KnownSecret` once secret-store substrate lands.
    Computes 32-byte hash via Microsoft's documented safe sync-over-async
    substrate-tier pattern:
    `Task.Run(() => argon2id.GetBytesAsync((int)options.HashLengthBytes)).GetAwaiter().GetResult()`
    (A5 LOAD-BEARING; Option B from the .NET-arch council's Q1 disposition —
    the `Task.Run` hop forces the awaited continuation to escape any caller's
    `SynchronizationContext` by hopping to a pool thread first, eliminating
    the Blazor-Server SynchronizationContext deadlock-class hazard regardless
    of Konscious's internal `ConfigureAwait` choice; cost is one additional
    thread-pool hop per signup, negligible against the ~50-100 ms Argon2id
    wall-clock; cites Microsoft's "How to: Implement synchronous methods using
    asynchronous methods" documented pattern). Formats the PHC string
    `$argon2id$v=19$m={m},t={t},p={p}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}`
    and returns. `VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)`:
    enforces input-length floors first (S6 sec-eng substrate amendment) —
    if `hashedPassword.Length > 1024` returns `Failed` without parsing (PHC
    strings are well under 200 chars; 1024 is a generous defense-in-depth
    ceiling); if `providedPassword.Length > 4096` returns `Failed`. Then
    inspects the `hashedPassword` prefix using `ReadOnlySpan<char>` slicing
    for allocation-free parsing on the hot path (C5 clarification):
    - **PHC string** (begins with `"$argon2id$"`): parses the format via a
      private `TryParsePhcString(ReadOnlySpan<char>, out Argon2idPhcParts)`
      helper; recomputes Argon2id with the parsed parameters + salt;
      constant-time-compares the recomputed hash bytes with the stored hash
      bytes via `CryptographicOperations.FixedTimeEquals(ReadOnlySpan<byte>, ReadOnlySpan<byte>)`
      (C6 — the .NET 11 Span-typed overload is the canonical surface; `byte[]`
      converts implicitly but explicit `ReadOnlySpan<byte>` typing is clearer);
      if match: if the parsed parameters meet OR exceed the current `options`
      floor, returns `PasswordVerificationResult.Success`; if any parsed
      parameter is below the corresponding `options` floor
      (`m < options.MemoryKib` OR `t < options.Iterations` OR
      `p < options.DegreeOfParallelism`), returns
      `PasswordVerificationResult.SuccessRehashNeeded` (parameter-floor upgrade
      trigger).
    - **Legacy V3 byte[] format** (Base64-decodes to non-zero-length array
      whose first byte is `0x01`): delegates to the privately-composed
      `_legacyHasher.VerifyHashedPassword(user, hashedPassword, providedPassword)`;
      on `PasswordVerificationResult.Success` returns
      `PasswordVerificationResult.SuccessRehashNeeded` (algorithm-upgrade
      trigger); on `Failed` returns `Failed`.
    - **Unrecognized / corrupt**: returns `PasswordVerificationResult.Failed`
      (do NOT throw — corrupt-hash on verify is an authentication failure,
      not a substrate error; matches BCL `PasswordHasher<TUser>` behavior).
    **Unified `SuccessRehashNeeded` trigger semantics (Q4 RESOLVED — CONFIRM).**
    Both the legacy-V3-success case (algorithm upgrade) and the
    below-floor-parameter-success case (parameter upgrade) return the same
    `PasswordVerificationResult.SuccessRehashNeeded` value. The unified trigger
    is the correct .NET-canonical design — the BCL `PasswordVerificationResult`
    enum is the contract surface; introducing a `PasswordVerificationResultExtended`
    would break the W#79 sec-eng A invariant (handler-tier code already
    switches on the BCL enum). The W#80 login handler treats both cases
    identically (rehash + persist). If a future audit-event-emission
    discrimination requirement surfaces (Q6 territory; application-tier per
    sec-eng disposition), the discrimination can be implemented at the W#80
    handler tier by parsing the stored hash format (PHC string vs legacy V3
    byte[]) before the `VerifyHashedPassword` call. NO
    `PasswordVerificationResultExtended` extension at substrate tier.
  - `Argon2idHashOptions.cs` — DI-bound options class with `init`-only
    setters (C2 clarification — `init`-only is the .NET-canonical compromise
    between `IConfiguration` binding compatibility and post-bind immutability;
    verified to work with `OptionsBuilder<T>.Bind` per the BCL mechanism):

    ```csharp
    public sealed class Argon2idHashOptions
    {
        public uint MemoryKib { get; init; } = 19456;          // OWASP minimum (Halt 1; Floor 3)
        public uint Iterations { get; init; } = 2;             // OWASP minimum (Halt 1; Floor 4)
        public uint DegreeOfParallelism { get; init; } = 1;    // OWASP minimum (Halt 1; Floor 5)
        public uint SaltLengthBytes { get; init; } = 16;       // Floor 1
        public uint HashLengthBytes { get; init; } = 32;       // Floor 2
        public byte[]? Pepper { get; init; }                   // null at MVP (Halt 7); ≤64 bytes when set (S6 / Floor 6)
    }
    ```

    Bound via standard `services.Configure<Argon2idHashOptions>(...)` or via
    the `AddSunfishPasswordHashing<TUser>(...)` extension's optional
    `Action<Argon2idHashOptions>? configure` parameter. Validated at bind time
    via `Argon2idHashOptionsValidator : IValidateOptions<Argon2idHashOptions>`
    (C3) and enforced at host startup via `Argon2idParameterFloorAssertion`
    (S3).
  - `MockPasswordHasher.cs` —
    `public sealed class MockPasswordHasher<TUser> : IPasswordHasher<TUser>, IMockPasswordHasher where TUser : class`.
    **`HashPassword(TUser user, string password)` returns the constant string
    `"mock-hash"`** (S2 sec-eng substrate amendment — zero password-derived
    material; the substrate-tier Floor 7 least-information-leakage discipline
    is satisfied by construction). The constant-string format embeds NO
    password-derived signal (no length, no first-byte, no character class
    proxy) — even when a load-test deployment legitimately ships with
    `SUNFISH_ALLOW_MOCK_PASSWORD_HASHER=true`, the stored mock-hashes carry
    zero cohort-attack credential-strength-histogram leakage.
    **`VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)`:**
    returns `Success` iff `hashedPassword == "mock-hash" && !string.IsNullOrEmpty(providedPassword)`;
    returns `Failed` otherwise. NEVER returns `SuccessRehashNeeded` — mock
    hashes are not subject to the parameter-floor upgrade discipline. This
    satisfies the SignupHandler unit-test surface's test-discrimination need
    (the handler depends only on whether HashPassword + VerifyHashedPassword
    round-trip succeeds; not on which password was hashed).
    **Log discipline (substrate-tier floor):** the mock MUST NOT log the
    plaintext password or any password-derived material (matches Floor 7 above).
    **Q2 RESOLVED — sec-eng-primary.** Constant-string format chosen over the
    length-leak alternative; the cohort-attack credential-strength-histogram
    concern is the load-bearing rationale.
  - `IMockPasswordHasher.cs` — empty marker interface; `MockPasswordHasher<TUser>`
    carries it. Xmldoc explicitly documents (a) the distinction from
    ADR 0096's `IMockVendorProvider` marker (PasswordHasher is Tier-1, not
    Tier-2 vendor); (b) the cross-tier reuse of ADR 0096's discipline pattern;
    (c) the discipline that future Tier-1 substrate mocks introduce their own
    marker families rather than sharing this one.
  - `MockPasswordHasherInProductionException.cs` — typed exception carrying the
    offending service type. Message format:
    `"Production-environment mock password hasher detected without opt-out. The following IPasswordHasher<TUser> registration is a mock concrete (IMockPasswordHasher): <type>. Either replace with a real Argon2idPasswordHasher registration via AddSunfishPasswordHashing<TUser>, or set SUNFISH_ALLOW_MOCK_PASSWORD_HASHER=true to acknowledge that this deployment intentionally ships with the mock."`.
  - `DependencyInjection/MockPasswordHasherProductionGuardAssertion.cs` —
    `public sealed class MockPasswordHasherProductionGuardAssertion : IHostedService`.
    Ctor: `(IServiceCollection services)` — captures the
    `IServiceCollection` reference at composition-root build time via the
    captured-snapshot pattern (matching ADR 0096's
    `MockProviderProductionGuardAssertion` precedent). The captured reference
    is effectively frozen post-`BuildServiceProvider` per
    `Microsoft.Extensions.DependencyInjection` convention (C4 clarification —
    mutating the collection after `Build` is technically allowed by the BCL
    but discouraged; ADR 0097 inherits ADR 0096's "treat the captured
    `IServiceCollection` as immutable post-Build" convention explicitly). On
    `StartAsync`:
    - If
      `string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Production", StringComparison.OrdinalIgnoreCase)`
      is false, return immediately.
    - If
      `bool.TryParse(Environment.GetEnvironmentVariable("SUNFISH_ALLOW_MOCK_PASSWORD_HASHER"), out var optOut) && optOut`
      is true, return immediately.
    - Else: iterate the captured `IServiceCollection` using the **canonical
      closed-generic discrimination idiom** (A3 LOAD-BEARING):

      ```csharp
      foreach (var descriptor in _capturedServices)
      {
          if (!descriptor.ServiceType.IsGenericType) continue;
          if (descriptor.ServiceType.GetGenericTypeDefinition() != typeof(IPasswordHasher<>)) continue;

          var concreteType = descriptor.ImplementationType
              ?? descriptor.ImplementationInstance?.GetType();
          if (concreteType is null) continue; // factory-only, out of scope (matches ADR 0096 discipline)
          if (!typeof(IMockPasswordHasher).IsAssignableFrom(concreteType)) continue;

          throw new MockPasswordHasherInProductionException(
              descriptor.ServiceType,
              concreteType);
      }
      ```

      Per generic-host behavior on hosted-service startup failure, the host's
      `RunAsync` throws and the process exits non-zero before serving the
      first request.
    - **Why the closed-generic check at the ServiceType, not the
      ImplementationType.** `typeof(IPasswordHasher<>).IsAssignableFrom(typeof(MockPasswordHasher<UserEntity>))`
      returns `false` (open-generic vs closed-generic assignability is
      asymmetric in .NET reflection). The discrimination MUST be on the
      descriptor's ServiceType via `IsGenericType` + `GetGenericTypeDefinition()
      == typeof(IPasswordHasher<>)`. The marker check
      (`typeof(IMockPasswordHasher).IsAssignableFrom(concreteType)`) is on
      the implementation type — which IS the correct assignability direction.
      This is a subtle .NET reflection trap; spec-tier discipline pre-empts
      it. ADR 0096's `MockProviderProductionGuardAssertion` does NOT face
      this complication because `IMockVendorProvider` is non-generic and the
      Tier-2 contracts (`IEmailProvider`, `ICaptchaVerifier`) are
      non-generic; ADR 0097 introduces the first fleet instance of a
      closed-generic-over-open-generic discrimination via `ServiceDescriptor`
      scan.
    - **Factory-only registrations are out of scope** — matches ADR 0096's
      `MockProviderProductionGuardAssertion` factory-only discipline
      (`packages/foundation-integrations/DependencyInjection/MockProviderProductionGuardAssertion.cs`
      lines 55-66 + 119-128). A factory registration like
      `services.AddSingleton<IPasswordHasher<UserEntity>>(_ => new MockPasswordHasher<UserEntity>())`
      bypasses the descriptor-scan (`ImplementationType` is null;
      `ImplementationInstance` is also null because the factory has not yet
      been invoked at the StartAsync timestamp). The spec MUST require
      type-based registration via the constrained
      `AddSunfishMockPasswordHashing<TUser>` helper to guarantee the
      assertion's scan sees the mock; the generic constraint on the helper
      (`where TUser : class` + the type-parameter-pair signature) prevents
      raw factory registration through the substrate's blessed entry point.
    - **Why `ServiceDescriptor` scan, not `IServiceProvider.GetServices<IMockPasswordHasher>()`**:
      marker-only interfaces are not resolvable from `IServiceProvider` unless
      the mock concretes are ALSO registered against `IMockPasswordHasher`
      directly (which they are not — they are registered as
      `IPasswordHasher<TUser>`). The `ServiceDescriptor.ImplementationType`
      scan is the only honest mechanism for inspecting "which registered
      services carry the marker." This mirrors ADR 0091 R2 A1 / ADR 0095 R2
      A3 / ADR 0096 R2 precedent: assertions inspect the registration tree,
      not the runtime resolution tree.
  - **`DependencyInjection/Argon2idParameterFloorAssertion.cs`** (S3 sec-eng
    substrate amendment) —
    `public sealed class Argon2idParameterFloorAssertion : IHostedService`.
    Ctor: `(IOptions<Argon2idHashOptions> options)` — captures the bound
    options snapshot at composition-root build time. On `StartAsync`: assert
    `options.MemoryKib >= 19456` (Floor 3); `options.Iterations >= 2`
    (Floor 4); `options.DegreeOfParallelism >= 1` (Floor 5; allow higher but
    not lower); `options.SaltLengthBytes >= 16` (Floor 1);
    `options.HashLengthBytes >= 32` (Floor 2); IF `options.Pepper is not null`,
    `options.Pepper.Length <= 64` (S6 / Floor 6 future-enablement pepper
    bound). On any violation, throw
    `Argon2idBelowFloorException(parameterName, expectedFloor, actualValue)`
    naming the offending parameter + expected floor + actual value. Per
    generic-host behavior on hosted-service startup failure, the host's
    `RunAsync` throws and the process exits non-zero before serving the
    first request. **This is the load-bearing write-path enforcement floor**
    — the substrate's non-substitutable-downward property (§"Cryptographic
    floor requirements" preamble) becomes substrate-tier-enforced at startup
    rather than documentation discipline. Composition-root misconfiguration
    of `Argon2idHashOptions.MemoryKib = 1024` (1 MiB; below the m=19456 KiB
    floor) is no longer silent — the assertion catches it at startup and
    the process exits before serving the first request. Closes the
    "parameter-downgrade foot-gun" by construction. Companion to the
    `Argon2idHashOptionsValidator : IValidateOptions<Argon2idHashOptions>`
    (C3 clarification) — the `IValidateOptions<T>` mechanism fires at first
    `IOptions<T>` resolution, which may be lazy; the `IHostedService`
    assertion fires unconditionally at host startup. Both are defense-in-depth
    against the same downgrade foot-gun.
  - **`Argon2idHashOptionsValidator.cs`** (C3 clarification) —
    `internal sealed class Argon2idHashOptionsValidator : IValidateOptions<Argon2idHashOptions>`.
    Implements `Validate(string? name, Argon2idHashOptions options)`
    returning `ValidateOptionsResult.Fail` listing every below-floor parameter
    + descriptive error message when any floor is violated; returns
    `ValidateOptionsResult.Success` otherwise. Registered automatically by
    `AddSunfishPasswordHashing<TUser>` via
    `services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<Argon2idHashOptions>, Argon2idHashOptionsValidator>())`.
    Provides the same floor enforcement as `Argon2idParameterFloorAssertion`
    at the `IOptions<T>` validation layer (companion defense-in-depth; the
    IHostedService is the host-startup catch, the validator is the
    options-resolution catch).
  - **`Argon2idBelowFloorException.cs`** (S3 sec-eng substrate amendment) —
    typed exception carrying `ParameterName`, `ExpectedFloor`, `ActualValue`
    properties. Message format: `"Argon2idHashOptions.{parameterName} value {actualValue} is below the substrate-tier floor of {expectedFloor}. Substrate parameters are non-substitutable downward per ADR 0097 §'Cryptographic floor requirements'. Either remove the configuration override or raise the value at-or-above the substrate floor."`.
  - `DependencyInjection/PasswordHashingServiceCollectionExtensions.cs`:
    **Three DI helpers — substrate-init separation per ADR 0096
    `AddSunfishVendorProviderSubstrate` precedent** (A6 LOAD-BEARING):
    - **`AddSunfishPasswordHashingSubstrate(this IServiceCollection services)`** —
      substrate-init helper invoked exactly once at composition-root assembly
      time. Registers `MockPasswordHasherProductionGuardAssertion` as
      `IHostedService` and `Argon2idParameterFloorAssertion` (S3 sec-eng
      substrate amendment) as `IHostedService`. Both registrations use
      `services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService>(...))`
      with factory-with-capture form so the assertions receive the captured
      `IServiceCollection` reference at composition-root build time
      (`TryAddSingleton<IHostedService, T>()` is REJECTED — it deduplicates on
      the `IHostedService` ServiceType alone, which would silently drop the
      second of two distinct hosted-service registrations; `TryAddEnumerable`
      deduplicates on the `(IHostedService, T)` pair, which is the correct
      dedup semantic for IHostedService registrations across multiple substrate
      packages). Matches ADR 0096's `AddSunfishVendorProviderSubstrate`
      pattern at
      `packages/foundation-integrations/DependencyInjection/VendorProviderServiceCollectionExtensions.cs`
      lines 66-75. Xmldoc explicitly documents the "substrate-init helper
      distinct from per-contract registration" convention.
    - **`AddSunfishPasswordHashing<TUser>(this IServiceCollection services, Action<Argon2idHashOptions>? configure = null, ServiceLifetime lifetime = ServiceLifetime.Singleton) where TUser : class`**
      (C1 — `ServiceLifetime` parameter for symmetry with ADR 0096's
      `AddSunfishVendorProvider<TContract, TConcrete>` signature; defaults to
      Singleton per G7): binds `IOptions<Argon2idHashOptions>` (applying
      `configure` if non-null) AND registers
      `Argon2idHashOptionsValidator : IValidateOptions<Argon2idHashOptions>` to
      fail-fast at startup when configured values fall below the substrate
      floors (C3 clarification + S3 sec-eng amendment substrate-tier
      backstop). Registers `Argon2idPasswordHasher<TUser>` as the
      `IPasswordHasher<TUser>` concrete via
      **`services.Replace(ServiceDescriptor.Describe(typeof(IPasswordHasher<TUser>), typeof(Argon2idPasswordHasher<TUser>), lifetime))`**
      (A4 LOAD-BEARING — NOT `services.AddSingleton`; the `Replace` semantics
      make the Step 2 composition-root cutover idempotent against any prior
      `IPasswordHasher<TUser>` registration — mirrors ADR 0096 R2 §D1c
      amendment #2 `services.Replace` discipline; closes the foot-gun where
      Engineer-author judgment about "delete the prior line first" would
      otherwise determine correctness). Argon2id is stateless per request
      (salt is generated per `HashPassword` call from the BCL RNG); the
      Singleton default lifetime is correct.
    - **`AddSunfishMockPasswordHashing<TUser>(this IServiceCollection services) where TUser : class`**:
      registers `MockPasswordHasher<TUser>` as the `IPasswordHasher<TUser>`
      concrete via `services.Replace(ServiceDescriptor.Singleton<IPasswordHasher<TUser>, MockPasswordHasher<TUser>>())`
      (same `services.Replace` discipline as the real-helper for the same
      reason — idempotency). The IHostedService production-guard assertion
      registration is NOT performed here — `AddSunfishPasswordHashingSubstrate`
      is the canonical single-point-of-registration for IHostedService
      assertions per A6. The xmldoc on `AddSunfishMockPasswordHashing<TUser>`
      explicitly directs callers to invoke `AddSunfishPasswordHashingSubstrate`
      first (or rely on a composition-root convention that does so).
      The generic constraint requires types implementing both
      `IPasswordHasher<TUser>` and `IMockPasswordHasher` — only mock concretes
      can register via this helper, closing the mock-without-marker
      compile-time foot-gun.
- Unit tests in `packages/foundation-password-hashing/tests/Sunfish.Foundation.PasswordHashing.Tests.csproj`:
  - `Argon2idPasswordHasherTests`:
    - hash + verify round-trip at OWASP minimum parameters (deterministic via
      fixed-salt test seam — option to inject `Func<byte[]> saltGenerator` for
      testability, or use a fixture-level deterministic seam).
    - hash + verify round-trip with `Pepper` set via `KnownSecret` (S1
      future-enablement test; verify pepper applied via Argon2's canonical
      secret-value surface — `argon2id.KnownSecret = options.Pepper` — and
      verify with same pepper succeeds while verify with different pepper
      fails; verify XOR-into-password-bytes is NOT the application path).
    - **RFC 9106 §5 reference-vector parity (≥3 vectors; S4 sec-eng substrate
      amendment).** Pin at least three reference vectors from the published
      RFC 9106 §5 test vectors (input password + salt + parameters → expected
      hash output). Verifies Konscious's adherence to the immutable spec;
      catches silent spec-drift on future Konscious bumps. Without these,
      a real bug-class that has shipped in cryptographic libraries before
      (e.g., Bouncy Castle spec-drift bugs) would not be caught at
      substrate tier.
    - **Argon2id primitive-selection assertion (S5 sec-eng substrate
      amendment).** Reflective verification that
      `Argon2idPasswordHasher<TUser>.HashPassword` instantiates
      `Konscious.Security.Cryptography.Argon2id` specifically (not `Argon2i`
      or `Argon2d`). The wire-format PHC prefix is hardcoded and does NOT
      cross-verify the primitive; a typo in the substrate code that
      instantiated the wrong variant would NOT be caught by current tests.
      Test pattern: type-substrate-assertion that the instantiated Argon2
      variant's `FullName` equals `"Konscious.Security.Cryptography.Argon2id"`.
      Without this floor, Floor 8's side-channel-resistance property is
      documentation discipline rather than substrate-tier-enforced;
      primitive-selection drift is the load-bearing risk.
    - **Input-length bounding (S6 sec-eng substrate amendment).**
      `hashedPassword.Length > 1024` returns `Failed` without parsing (PHC
      strings are well under 200 chars; defense-in-depth ceiling);
      `providedPassword.Length > 4096` returns `Failed` on verify and throws
      `ArgumentException` on hash (substrate-tier complement to the W#79
      handler-tier input-length cap); `Argon2idHashOptions.Pepper.Length > 64`
      throws via `Argon2idParameterFloorAssertion` at startup.
    - parameter-floor upgrade: verify a hash made at m=19456 against an
      `Argon2idHashOptions` floor of m=46080 returns `SuccessRehashNeeded`
      (not `Success`).
    - legacy V3 PBKDF2 verify: pre-compute a known V3 hash via the BCL
      `PasswordHasher<TUser>`; verify against `Argon2idPasswordHasher<TUser>`
      returns `SuccessRehashNeeded` on success / `Failed` on wrong password.
    - PHC string parse robustness: malformed prefixes / wrong algorithm
      tokens / truncated salt/hash sections / non-base64 sections all return
      `Failed` (no throw).
    - constant-time comparison: verify the comparison path uses
      `CryptographicOperations.FixedTimeEquals` (test pattern: assert
      reflection over the verify method body OR use a behavior-based test
      that's tolerant of timing variance — the substrate-tier assertion is
      that the BCL primitive is used, not a timing-equivalence empirical test
      that would be flaky in CI).
    - Blazor-Server SynchronizationContext deadlock-safety (A5 LOAD-BEARING).
      Test pattern: invoke `HashPassword` from a thread whose
      `SynchronizationContext.Current` is set to a single-thread dispatcher
      (simulating Blazor Server); assert the call completes without deadlock.
      The `Task.Run` hop is the load-bearing mechanism.
  - `MockPasswordHasherTests`:
    - hash + verify round-trip with constant-string mock format
      (`"mock-hash"`; S2 sec-eng substrate amendment).
    - verify fails on empty providedPassword.
    - verify fails on mismatched hashedPassword (non-`"mock-hash"`).
    - verify never returns `SuccessRehashNeeded` (mock is not subject to
      parameter-floor upgrade).
    - mock NEVER logs the plaintext password (test via log-capture fixture).
    - **mock format MUST NOT embed any password-derived material** — assert
      via reflection that the stored hash string is literally `"mock-hash"`
      regardless of input password (S2 — verifies Floor 7 by construction).
  - `MockPasswordHasherProductionGuardAssertionTests`:
    - non-prod bypass (`ASPNETCORE_ENVIRONMENT=Development`).
    - opt-out env-var bypass (`SUNFISH_ALLOW_MOCK_PASSWORD_HASHER=True` /
      `TRUE` / `false`; non-parseable values `1` / `yes` / `on` are
      fail-closed = treat as no-opt-out).
    - prod-with-mock-no-opt-out throws `MockPasswordHasherInProductionException`
      naming the offending service type.
    - prod-with-real-only passes.
    - **closed-generic discrimination idiom (A3 LOAD-BEARING).** Test
      pattern: register a non-`IPasswordHasher<>` mock service alongside a
      mock `IPasswordHasher<UserEntity>` registration; assert the assertion
      scans correctly via `IsGenericType + GetGenericTypeDefinition() ==
      typeof(IPasswordHasher<>)` and only flags the password-hasher mock,
      not the unrelated service. Verifies the asymmetric open-vs-closed
      generic assignability discipline.
    - integration test: `WebApplicationBuilder().Build()` with
      `AddSunfishPasswordHashingSubstrate()` +
      `AddSunfishMockPasswordHashing<UserEntity>()` +
      `ASPNETCORE_ENVIRONMENT=Production` (no opt-out) asserts
      `IHost.StartAsync` throws `MockPasswordHasherInProductionException`.
    - integration test: same fixture but with
      `SUNFISH_ALLOW_MOCK_PASSWORD_HASHER=true`, asserts `IHost.StartAsync`
      succeeds.
    - **Test isolation discipline:** env-var reads are process-global state
      and can leak across xUnit test parallelization. Tests use either an
      `IEnvironment` abstraction injected into the assertion OR an
      `IDisposable` env-restoration helper that captures + restores
      `ASPNETCORE_ENVIRONMENT` + `SUNFISH_ALLOW_MOCK_PASSWORD_HASHER` across
      the test lifecycle (xUnit `[Collection]` discipline or
      constructor/Dispose pair) — same pattern as ADR 0096 Step 1 tests.
  - **`Argon2idParameterFloorAssertionTests`** (S3 sec-eng substrate
    amendment) — each floor parameter has below-floor / at-floor /
    above-floor coverage:
    - `MemoryKib = 1024` (below Floor 3) throws
      `Argon2idBelowFloorException` naming `MemoryKib`, `expectedFloor=19456`,
      `actualValue=1024`.
    - `MemoryKib = 19456` (at Floor 3) passes.
    - `MemoryKib = 46080` (above Floor 3) passes.
    - same shape for `Iterations` (Floor 4; floor 2), `DegreeOfParallelism`
      (Floor 5; floor 1), `SaltLengthBytes` (Floor 1; floor 16),
      `HashLengthBytes` (Floor 2; floor 32), `Pepper.Length` (Floor 6
      future-enablement bound; ceiling 64).
    - integration test: `WebApplicationBuilder().Build()` with
      `AddSunfishPasswordHashingSubstrate()` +
      `AddSunfishPasswordHashing<UserEntity>(o => o.MemoryKib = 1024)` +
      `ASPNETCORE_ENVIRONMENT=Production` asserts `IHost.StartAsync` throws
      `Argon2idBelowFloorException`.
  - **`Argon2idHashOptionsValidatorTests`** (C3 clarification) — defense-in-
    depth at the `IOptions<T>` validation layer:
    - each below-floor parameter case yields
      `ValidateOptionsResult.Fail` with a descriptive message.
    - at-floor and above-floor cases yield `ValidateOptionsResult.Success`.
    - multiple simultaneous below-floor parameters yield a single
      `ValidateOptionsResult.Fail` listing all violations.
  - `PasswordHashingServiceCollectionExtensionsTests`:
    - `AddSunfishPasswordHashingSubstrate()` registers
      `MockPasswordHasherProductionGuardAssertion` AND
      `Argon2idParameterFloorAssertion` as `IHostedService` exactly once
      (A6 LOAD-BEARING; `TryAddEnumerable` semantics — calling twice yields
      one of each, not two of each).
    - `AddSunfishPasswordHashing<UserEntity>()` registers
      `Argon2idPasswordHasher<UserEntity>` as
      `IPasswordHasher<UserEntity>` singleton via `services.Replace`.
    - `AddSunfishPasswordHashing<UserEntity>(o => o.MemoryKib = 46080)`
      applies the configure delegate.
    - `AddSunfishPasswordHashing<UserEntity>` `services.Replace` idempotency
      (A4 LOAD-BEARING): register the BCL `PasswordHasher<UserEntity>` first;
      then call `AddSunfishPasswordHashing<UserEntity>`; assert only the
      Argon2id concrete is registered (the BCL line is displaced).
    - `AddSunfishMockPasswordHashing<UserEntity>()` registers the mock via
      `services.Replace`.
    - calling both `AddSunfishMockPasswordHashing<UserEntity>` AND
      `AddSunfishMockPasswordHashing<OtherUser>` after a single
      `AddSunfishPasswordHashingSubstrate` call yields one of each
      production-guard assertion + floor assertion (not two of each).
    - `AddSunfishPasswordHashing<UserEntity>(lifetime: ServiceLifetime.Scoped)`
      (C1) registers the concrete with the requested lifetime.
- Documentation:
  - xmldoc on every introduced type per ADR 0069 §A0 discipline.
  - `IMockPasswordHasher` xmldoc explicitly names (a) the distinction from
    ADR 0096's `IMockVendorProvider`; (b) the cross-tier reuse of ADR 0096's
    pattern; (c) the future-substrate guidance (introduce your own marker
    family rather than sharing this one).
  - `Argon2idHashOptions` xmldoc cross-references OWASP cheatsheet 2026-05-27
    parameter recommendations + cites RFC 9106 §4.

**Council review (Halt 9 RATIFY): MANDATORY dual-council at PR-open.**
.NET-architect council reviews the substrate-package geometry, the DI helper
shape, the IHostedService assertion mechanism, the `Argon2idHashOptions`
binding shape, and the synchronous-over-`GetBytesAsync` interop pattern (the
Konscious API exposes async; the BCL `IPasswordHasher<TUser>` interface is
synchronous — the substrate bridges via `.GetAwaiter().GetResult()` per
Microsoft's documented sync-over-async-acceptable pattern at substrate-tier
single-thread-pool boundaries). Security-engineering council reviews the
cryptographic-primitive correctness, the PHC string format conformance, the
`CryptographicOperations.FixedTimeEquals` discipline, the
no-log-password floors, the parameter-floor upgrade mechanism, the
opt-out env-var fail-closed parsing semantics, and the production-default
invariant.

### Step 2 — W#79 SignupHandler composition-root cutover PR

Branch shape: `feat/adr-0097-step-2-w79-argon2id-cutover` (Engineer-authored
after Step 1 merges + W#79 sub-cohort 1 reaches the production-prep gate).

Scope (one-line composition-root substitution; zero handler-tier changes):

In `Sunfish.Bridge.Onboarding.DependencyInjection` (or wherever the W#79
substrate registration lives at cutover time), the cutover adds two lines:

```csharp
// ADR 0097 production cutover (Argon2id at OWASP minimum)
services.AddSunfishPasswordHashingSubstrate();  // IHostedService assertions (A6 LOAD-BEARING)
services.AddSunfishPasswordHashing<UserEntity>(options =>
{
    options.MemoryKib = 19456;        // OWASP minimum (Halt 1)
    options.Iterations = 2;
    options.DegreeOfParallelism = 1;
});
```

**The W#79 MVP-era `services.AddSingleton<IPasswordHasher<UserEntity>,
PasswordHasher<UserEntity>>()` line MAY be deleted at cutover time as code-
review hygiene, but is NOT required to be deleted** (A4 LOAD-BEARING — the
`services.Replace` semantics inside `AddSunfishPasswordHashing<TUser>`
displace any prior `IPasswordHasher<UserEntity>` registration idempotently;
the substitution is correctness-stable regardless of Engineer-author
judgment about deletion order).

The substitution is **mechanical**. The W#79 sec-eng A + .NET-arch K1
invariant ("interface, NEVER static; ADR 0097 future Argon2id swap requires
zero callsite changes") is preserved by construction — the SignupHandler ctor
signature is unchanged; the `HashPassword(stubUser, request.Password)` callsite
is unchanged; only the DI registration changes.

Tests:
- W#79 SignupHandler integration tests continue to pass with the substituted
  registration (no test-code changes; the swap is opaque to the handler).
- New integration test in the onboarding cluster: assert that a freshly-signed-
  up user's `User.PasswordHash` field begins with `"$argon2id$"` (verifying
  the cutover took effect end-to-end).
- W#79 sec-eng D constant-work latency test: re-tune the ±25% p50/p95 latency
  parity floor if Argon2id-at-OWASP-minimum's wall-clock cost (~50-100 ms)
  shifts the calibration window from PBKDF2-100k's wall-clock cost. Per
  scaffold §2.2 Gap 4: the W#79 sec-eng SPOT-CHECK author is the integration-
  test threshold owner.

**Council review (Halt 9 RATIFY): sec-eng MANDATORY SPOT-CHECK at PR-open.**
Sec-eng council reviews the composition-root cutover correctness (no inadvertent
introduction of the mock; no callsite drift from the W#79 Rev 2 invariant; the
W#79 sec-eng D constant-work timing parity re-tune is accurate against the new
algorithm). .NET-architect SPOT-CHECK is OPTIONAL at Step 2 — the substitution
is mechanical and the substrate-package surface was already attested at Step 1.

### Step 3 — (NOT pursued) Background bulk-rehash job

Per Halt 5 disposition: REJECTED at MVP and indefinitely. Bulk-rehash is
**physically impossible without retrievable plaintext** — passwords are not
stored in plaintext; the hash is a one-way function. Any "bulk-rehash" path
would necessarily be a "force password reset by email" UX flow, not a substrate
operation. If a force-password-reset cohort becomes necessary (e.g., compliance
audit reveals weaker-than-floor hashes for a specific user cohort), it would
ship as a W#80+ application-tier flow, NOT as an ADR 0097 substrate step.

### Forward integration — W#80 login handler observes `SuccessRehashNeeded`

NOT delivered by this ADR; documented for cross-reference. W#80 sub-cohort 1's
login handler consumes the `PasswordVerificationResult.SuccessRehashNeeded`
trigger:

```csharp
// W#80 login handler (forward-watch; not in ADR 0097 scope)
var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
if (result == PasswordVerificationResult.Failed) return Unauthorized();
if (result == PasswordVerificationResult.SuccessRehashNeeded)
{
    var freshHash = _passwordHasher.HashPassword(user, request.Password);
    await _userRepository.UpdatePasswordHashAsync(user.Id, freshHash, cancellationToken);
}
// proceed with login success ...
```

This is the lazy-rehash-on-next-login mechanism per Halt 5 / Halt 10. ADR 0097
provides the trigger primitive; W#80 ships the consumer integration. Audit-event
emission on rehash (`user_password_hash_upgraded`) is an ADR 0049 enum-extension
candidate forwarded to W#80 territory.

## Alternatives considered (rejections)

**Alt A — Keep PBKDF2 V3 (Option A in §Considered options).** REJECTED per
Halt 1 / Halt 8 disposition. OWASP cheatsheet 2026-05-27: PBKDF2 acceptable
only for FIPS-mandated environments; Sunfish has no FIPS-mandate. The W#79 H8
commitment forecloses this option.

**Alt B — Migrate to bcrypt (Option B in §Considered options).** REJECTED.
bcrypt is OWASP's "fallback when Argon2id unavailable" tier; Argon2id IS
available via Konscious. No compensating advantage over Argon2id.

**Alt C — Argon2id with non-generic `IPasswordHasher` wrapper (Option D in
§Considered options).** REJECTED per D2 / Halt 4 rationale. Violates W#79
sec-eng A invariant (would introduce a handler-tier callsite change);
speculative future-proofing for "if Sunfish ever drops ASP.NET Identity
entirely."

**Alt D — Argon2id with full ADR 0096 Tier-2 vendor-substrate discipline
(Option E in §Considered options).** REJECTED per D1 / Halt 4 rationale.
Argon2id is not a vendor surface; Tier-2 vendor-substrate pattern presumes a
swap surface that doesn't exist for cryptographic primitives. Cross-tier
reuse of the mock-first discipline at Tier-1 (Option F) is the cleaner move
— borrows the discipline pattern without conflating the substrate semantics.

**Alt E — Skip the mock entirely; tests use the real
`Argon2idPasswordHasher<TUser>` with reduced parameters (m=1024, t=1, p=1)
for speed.** REJECTED per Halt 4 rationale. Reduced-parameter Argon2id at
m=1024 KiB / t=1 / p=1 is still tens-of-milliseconds per hash on cold-cache;
the mock's deterministic no-op shape is meaningfully faster at scale. More
importantly, "use the real algorithm at reduced parameters" makes tests
exercise the algorithm-correctness path that is better covered by dedicated
cryptographic unit tests; the SignupHandler unit-test surface benefits from
algorithm-opacity, not algorithm-correctness.

**Alt F — Co-locate in `packages/foundation-authorization/` (Sub-option β).**
REJECTED per Halt 3. `foundation-authorization` is authorization-context
resolution; PasswordHasher is authentication-credential-storage. Cluster
confusion is the load-bearing concern.

**Alt G — Co-locate in `packages/foundation-integrations/` (Sub-option γ).**
REJECTED per Halt 3. `foundation-integrations` is provider-neutrality for
external vendor surfaces; Argon2id is an internal cryptographic primitive.

**Alt H — Enable pepper at MVP via
`IConfiguration["Sunfish:Argon2id:Pepper"]`.** REJECTED per Halt 7. Pepper is
optional defense-in-depth per OWASP cheatsheet; pepper rotation + compromise
semantics require a secret-store substrate that does not yet exist. The
`Argon2idHashOptions.Pepper` nullable field is reserved for future enablement
once the secret-store substrate ADR lands.

**Alt I — Eager bulk-rehash background job.** REJECTED. Physically impossible
without retrievable plaintext.

**Alt J — Hybrid: lazy-on-login + force-password-reset email to all users with
PBKDF2 hashes within N days.** REJECTED at MVP. UX cost (forced password
reset annoys users) without compensating compliance posture (PBKDF2-100k is
OWASP-acceptable; the upgrade improves margin, not closes a vulnerability).
May be revisited as a W#80+ application-tier flow if a compliance audit
demands it.

**Alt K — `IOptionsMonitor<Argon2idHashOptions>` runtime-reloadable.**
REJECTED per Halt 11 / D7. Substrate-tier parameter changes are
deployment-tier events; runtime reload introduces a mid-running window where
hashes-in-flight use different parameters than newly-issued ones,
complicating `SuccessRehashNeeded` discrimination.

## Consequences

**Positive:**

- **Argon2id production-safety is codified at substrate tier**, not
  documentation discipline. The W#79 H8 commitment is satisfied by construction:
  Step 2 composition-root substitution makes Argon2id the default; Step 1
  substrate package provides the algorithm + the production-safety invariants.
- **Lazy migration is automatic.** `SuccessRehashNeeded` is the canonical
  trigger; W#80's login handler observes + rehashes; existing PBKDF2-V3 hashes
  upgrade on next login without user-visible operation. Bulk-rehash is
  physically impossible; lazy is the only safe path.
- **Self-describing PHC string format supports future parameter hardening
  without forced reset.** When the substrate-tier floor rises (m=19 → m=46 →
  m=64), the verify path detects below-floor hashes via embedded parameter
  inspection + returns `SuccessRehashNeeded` automatically.
- **Mock-first discipline at Tier-1 unlocks fast-test-loop substantially.**
  SignupHandler unit tests avoid Argon2id's tens-of-milliseconds-per-hash cost;
  fleet-scale test suite penalty avoided. The
  `MockPasswordHasherProductionGuardAssertion` closes the silent-mock-leak
  foot-gun permanently.
- **Cross-tier reuse of ADR 0096's discipline pattern is the cheapest
  application of the mock-first invariant at Tier-1.** ~80-100 LOC investment
  for permanent foot-gun closure; no Tier-2 vendor-substrate conflation.
- **The W#79 sec-eng A + .NET-arch K1 invariant is preserved by construction.**
  Step 2 is a one-line composition-root substitution with zero handler-tier
  callsite changes; the substrate's value proposition is exactly this property.
- **Future password-policy primitives have a clean cluster home.**
  `packages/foundation-password-hashing/` is the substrate-tier home for
  password-related primitives; future HIBP integration or password-strength
  meter primitives can co-locate without further cluster decisions.

**Negative / costs:**

- **One new package** (`packages/foundation-password-hashing/`) ships at
  pre-production. Engineering hours: ~2-3 days for Step 1 (substrate +
  comprehensive tests); ~0.5-1 day for Step 2 (one-line substitution + W#79
  sec-eng D timing re-tune + integration assertion).
- **New external NuGet dependency**
  (`Konscious.Security.Cryptography.Argon2` v1.3.1). Supply-chain risk
  mitigated by MIT license + OWASP endorsement + ~700k downloads + no
  transitive runtime dependencies beyond BCL.
- **Argon2id at OWASP-minimum parameters costs ~50-100 ms per `HashPassword`
  call** vs PBKDF2-100k's ~10-30 ms. Wall-clock budget impact at the
  SignupHandler tier; absorbed by the W#79 sec-eng D constant-work discipline
  (unconditional cost on every request); the integration-test latency
  threshold may need re-tuning post-Step-2-cutover per scaffold §2.2 Gap 4.
- **Dual-council MANDATORY review on this ADR + Step 1 PR** adds ~30-min
  dispatch latency per PR. Pre-paid against the Rev-2-with-strengthening
  churn pattern ADR 0095 / ADR 0096 / ADR 0098 demonstrated.
- **Mocks ship in the same substrate package as contracts.** Non-Sunfish
  consumers (none today) would take a small dependency on the mock + the
  production-guard assertion they may never use. Marginal; not a real cost.

**Risks:**

- **Risk R1 — Step 1 PR scope.** Step 1 covers a lot (Argon2id concrete +
  mock + marker + production-guard IHostedService + DI helpers + options
  binding + comprehensive tests). Engineer may split into Step 1a
  (substrate types) + Step 1b (mock-first discipline) if scope threshold
  reached. Mitigation: explicit Step 1a/1b decomposition in the W#79
  hand-off authoring if Engineer flags.

- **Risk R2 — Sync-over-async on `Konscious.Argon2id.GetBytesAsync`.
  MITIGATED in Rev 2.** The BCL `IPasswordHasher<TUser>.HashPassword`
  interface is synchronous; Konscious exposes Argon2id hash computation as
  async (`GetBytesAsync`). The substrate bridges via
  `Task.Run(() => argon2id.GetBytesAsync(...)).GetAwaiter().GetResult()`
  per Microsoft's documented safe sync-over-async substrate-tier pattern
  (A5 LOAD-BEARING; .NET-architect Q1 disposition Option B). The
  `Task.Run` hop forces the awaited continuation to escape any caller's
  `SynchronizationContext` by hopping to a pool thread first, eliminating
  the Blazor-Server SynchronizationContext deadlock-class hazard
  regardless of Konscious's internal `ConfigureAwait` choice. Substrate-
  tier cost: one additional thread-pool hop per signup; negligible against
  the ~50-100 ms Argon2id wall-clock. Step 1 PR includes a deadlock-safety
  test invoking `HashPassword` from a thread whose
  `SynchronizationContext.Current` is set to a single-thread dispatcher
  (simulating Blazor Server) and asserts the call completes without
  deadlock.

- **Risk R3 — `RandomNumberGenerator.GetBytes` thread-safety + RNG-seed
  freshness.** The BCL `RandomNumberGenerator` is thread-safe + uses the OS
  CSPRNG; per-call seeding is automatic. No mitigation needed; substrate-tier
  assertion verifies the BCL primitive is used.

- **Risk R4 — Production-guard false-positive when load-tests deploy with
  the mock intentionally.** Mitigation:
  `SUNFISH_ALLOW_MOCK_PASSWORD_HASHER=true` opt-out env var is the
  documented escape hatch; load-test infrastructure docs must reference it
  (parallel to ADR 0096's `SUNFISH_ALLOW_MOCK_PROVIDERS` documentation).

- **Risk R5 — Argon2id wall-clock cost at OWASP-minimum exceeds W#79 latency
  budget on a degraded host.** Argon2id is memory-bound; a host under
  memory pressure may pay 2-5× the nominal hash cost. Mitigation: W#79
  sec-eng D integration-test latency budget re-tuned at Step 2; if the
  budget is unachievable at OWASP-minimum parameters on production-class
  hardware, the substrate parameters tunable via `Argon2idHashOptions`
  allow temporary hardening reduction (but NOT below OWASP minimum per
  Floor 3) pending host capacity uplift. Future ADR amendment if persistent.

## Open questions (Rev 2 — all seven RESOLVED via dual-council attestation)

The seven Q's from Rev 1 are RESOLVED via the .NET-architect + sec-eng
dual-council AMBER verdicts. Rev 2 folds the dispositions into the spec
text above; the questions remain in the ADR for traceability.

**Q1 — Sync-over-async on `Konscious.Argon2id.GetBytesAsync` from synchronous
`IPasswordHasher<TUser>.HashPassword`. RESOLVED — Option B (Task.Run hop).**
.NET-architect Q1 disposition: the substrate uses
`Task.Run(() => argon2id.GetBytesAsync(...)).GetAwaiter().GetResult()` per
Microsoft's documented safe sync-over-async substrate-tier pattern (A5
LOAD-BEARING). The `Task.Run` hop forces the awaited continuation to escape
any caller's `SynchronizationContext` by hopping to a pool thread first,
eliminating the Blazor-Server SynchronizationContext deadlock-class hazard
regardless of Konscious's internal `ConfigureAwait` choice. Cost is one
additional thread-pool hop per signup; negligible against the ~50-100 ms
Argon2id wall-clock. Sec-eng Q1-adjacent: substrate-tier safety property
holds because Argon2id is memory-bound (no actual I/O wait inside the
async); the sync-over-async pattern is safe for THIS substrate but the
property is non-obvious — Option B closes the deadlock-class hazard by
construction. See §"Implementation roadmap Step 1"
`Argon2idPasswordHasher.HashPassword` spec.

**Q2 — `MockPasswordHasher.HashPassword` deterministic format. RESOLVED —
constant string `"mock-hash"`.** Sec-eng-primary Q2 disposition: zero
password-derived material in mock format (S2 sec-eng substrate amendment).
The constant-string format embeds NO password-derived signal — even when
a load-test deployment legitimately ships with
`SUNFISH_ALLOW_MOCK_PASSWORD_HASHER=true`, the stored mock-hashes carry
zero cohort-attack credential-strength-histogram leakage. The
length-bucket alternative was rejected; the substrate-tier discipline is
zero-side-channel-leakage, not minimal-side-channel-leakage. .NET-architect
C9 concurs. See §"Implementation roadmap Step 1" `MockPasswordHasher.cs`
spec.

**Q3 — `Argon2idHashOptions.Pepper` future-enablement shape. RESOLVED —
Argon2 `KnownSecret` (RFC 9106 §3.1 `K` parameter).** Sec-eng-primary Q3
disposition: pepper application uses Konscious's `Argon2id.KnownSecret`
property — Argon2's canonical secret-value surface that mixes the secret
into the hash construction in a cryptographically well-defined manner (S1
sec-eng substrate amendment). The XOR-into-password-bytes pattern in Rev 1
was rejected as non-canonical (undefined semantics for non-overlapping
ranges; reinvents what the algorithm natively supports). HMAC-SHA256(pepper,
password)-then-Argon2id is an alternative but uses the secret outside the
primitive; substrate-tier discipline prefers the in-primitive application.
See §"Implementation roadmap Step 1" `Argon2idPasswordHasher.HashPassword`
spec + §"Cryptographic floor requirements" Floor 9.

**Q4 — PHC string parameter-floor upgrade vs algorithm-version upgrade
discrimination. RESOLVED — CONFIRM unified-trigger treatment.** .NET-
architect Q4 disposition: the BCL `PasswordVerificationResult` enum is the
contract surface; introducing a `PasswordVerificationResultExtended` would
break the W#79 sec-eng A invariant (handler-tier code already switches on
the BCL enum). The unified trigger is the correct .NET-canonical design —
both upgrade classes have the same handler-tier response (call
`HashPassword(...)` again + persist the result). If a future audit-event-
emission discrimination requirement surfaces (Q6 territory; application-
tier per sec-eng disposition), the discrimination can be implemented at the
W#80 handler tier by parsing the stored hash format (PHC string vs legacy
V3 byte[]) before the `VerifyHashedPassword` call. NO
`PasswordVerificationResultExtended` extension at substrate tier. See
§"Implementation roadmap Step 1" `Argon2idPasswordHasher.VerifyHashedPassword`
spec.

**Q5 — Argon2id default parameter floor calibration. RESOLVED — m=19 MiB
correct at MVP.** Sec-eng-primary Q5 disposition: m=19 MiB is the right
substrate default at MVP per OWASP cheatsheet 2026-05-27. Raising
preemptively to m=46 MiB without a hardware-capacity uplift creates
needless signup-latency cost without compensating security gain at MVP
scale. The parameter-floor upgrade mechanism via `SuccessRehashNeeded`
makes future hardening a deployment-tier operation, not a forced-reset
event. Deployment-tier tuning via `Argon2idHashOptions.MemoryKib` per
deployment is the canonical hardening path. Forward-watch on OWASP
cheatsheet floor-raise (Revisit Trigger #1). See §"Cryptographic floor
requirements" Floor 3.

**Q6 — Audit-event emission on hash upgrade. RESOLVED — application-tier
(NOT substrate-tier).** Sec-eng-primary Q6 disposition: substrate-no-
audit-emission precedent per ADR 0095 / ADR 0096 (sister substrate ADRs
do not own `IAuditEventEmitter` dispatch). The W#80 login handler is the
correct tier for emitting `user_password_hash_upgraded` on
`SuccessRehashNeeded`-then-rehash. ADR 0097 substrate does NOT participate
in audit-event emission; this is a deliberate substrate-tier discipline
(substrates own primitives; handlers own audit). Forward-watch: ADR 0049
enum extension at W#80 hand-off (`user_password_hash_upgraded` audit event
constant).

**Q7 — Step 1 csproj package-ref strategy. RESOLVED — 5 PackageReferences
(Abstractions-only set + NU1510 NoWarn).** .NET-architect Q7 disposition:
the corrected minimal set is
`Microsoft.AspNetCore.Identity.Core` (per A2 CPM addition) +
`Konscious.Security.Cryptography.Argon2` (per Halt 2) +
`Microsoft.Extensions.DependencyInjection.Abstractions` (for
`ServiceDescriptor` / `IServiceCollection` / `services.Replace` /
`TryAddEnumerable` — all live in the Abstractions surface) +
`Microsoft.Extensions.Hosting.Abstractions` (for `IHostedService`) +
`Microsoft.Extensions.Options` (for `IOptions<Argon2idHashOptions>`). The
`Microsoft.Extensions.Options.ConfigurationExtensions` reference from Rev 1
is NOT required at the substrate tier — configuration binding via
`IConfiguration.GetSection(...)` happens at the composition root, not in
the substrate package. The `Microsoft.Extensions.DependencyInjection`
(non-Abstractions) reference from Rev 1 is also NOT required. TFM is
`net11.0` per A1 (fleet convention; no Engineer-author choice). NU1510
NoWarn (`<NoWarn>CS1591;NU1510</NoWarn>`) per the
`foundation-authorization` precedent. See §"Implementation roadmap Step 1"
csproj scope.

## Revisit triggers

This ADR is revisited (Rev 2 or follow-up amendment) when any of:

1. **OWASP cheatsheet parameter floor rises** (m=19 → m=46 → m=64 etc.). The
   substrate-tier default in `Argon2idHashOptions` is updated; existing
   hashes auto-upgrade via `SuccessRehashNeeded` on next login per D6 /
   §"Migration mechanism." ADR amendment carries the new floor +
   cross-references the OWASP guidance source.
2. **A FIPS-mandate posture is adopted by Sunfish.** Argon2id is not
   FIPS-validated; the substrate would need a FIPS-mode toggle that falls
   back to PBKDF2-HMAC-SHA256. FORWARD-WATCH; out of ADR 0097 R1 scope.
3. **Secret-store substrate ADR lands** (currently does not exist).
   `Argon2idHashOptions.Pepper` future-enablement would amend ADR 0097 to
   require the pepper substrate dependency + define rotation semantics.
4. **A second Tier-1 substrate adopts the mock-first discipline.** The
   cross-tier reuse pattern would amend ADR 0097's §"Substrate / layering
   notes" cross-tier mock-marker family discipline to add the new substrate
   as a worked example.
5. **`Konscious.Security.Cryptography.Argon2` is deprecated, archived, or
   has a security advisory.** Substrate-tier dependency posture change; ADR
   amendment names the replacement library + migration path.
6. **The `IPasswordHasher<TUser>` BCL interface is deprecated by
   ASP.NET Core.** ASP.NET Identity roadmap is stable as of 2026-05-27;
   future deprecation would amend ADR 0097's interface-retention decision.
7. **W#80 login handler integration surfaces a new substrate requirement.**
   E.g., audit-event emission from substrate (Q6 council resolution may go
   either way); per-tenant parameter override (out-of-scope at MVP;
   forward-watch).

## References

- ONR scaffold (predecessor research): `shipyard/icm/01_discovery/research/onr-adr-0097-passwordhasher-substrate-scaffold.md` (1167 lines; via the sibling shipyard PR 167; the scaffold was moved to the canonical ADR location in this same branch per the ADR 0096 R2 / ADR 0098 R2 same-branch authoring pattern)
- ONR status beacon: `coordination/inbox/onr-status-2026-05-27T1428Z-adr-0097-passwordhasher-substrate-scaffold-complete.md`
- Admiral 11-halt ruling: `coordination/inbox/admiral-ruling-2026-05-27T1430Z-adr-0097-passwordhasher-11-halts-resolved.md`
- W#79 H8 disposition (this ADR's source trigger): `coordination/inbox/admiral-ruling-2026-05-26T0035Z-w79-stage-05-9-halts-resolved.md`
- W#79 Stage-05 hand-off Rev 2 (sec-eng A + .NET-arch K1 + sec-eng D invariants): `shipyard/.worktrees/onr-w79-stage-05-handoff/icm/05_implementation-plan/cohort-w79-hand-off.md`
- ADR 0013 Foundation.Integrations (provider-neutrality discipline; spirit-not-letter for this substrate): `docs/adrs/0013-foundation-integrations.md`
- ADR 0049 AuditEvent (forward-watch — `user_password_hash_upgraded` enum-extension candidate at W#80 layer): `docs/adrs/0049-audit-event.md`
- ADR 0091 ITenantContext Divergence Resolution (R2 / A1 startup-assertion precedent for the Tier-1 mock production-guard): `docs/adrs/0091-itenantcontext-divergence-resolution.md`
- ADR 0095 Bootstrap Context (substrate-tier ADR cadence + SignupHandler runs in bootstrap scope): `docs/adrs/0095-bootstrap-context.md`
- ADR 0096 Tier-2 Vendor-Provider Substrate (cross-tier mock-first discipline pattern; distinct marker family): `docs/adrs/0096-tier-2-vendor-provider-substrate.md`
- ADR 0098 Block-Naming Generalization (substrate-tier ADR Rev 2 cadence precedent; dual-council MANDATORY): `docs/adrs/0098-block-naming-generalization.md`
- OWASP Password Storage Cheat Sheet (retrieved 2026-05-27): https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html
- OWASP Application Security Verification Standard 4.0.3 §2.4 "Credential Storage Requirements" (2024-10): https://owasp.org/www-project-application-security-verification-standard/
- IETF RFC 9106 "Argon2 Memory-Hard Function for Password Hashing and Proof-of-Work Applications" (2021-09): https://datatracker.ietf.org/doc/rfc9106/
- NIST SP 800-63B-3 "Digital Identity Guidelines: Authentication and Lifecycle Management" §5.1.1.2 (2017-06; errata current 2026-05-27): https://pages.nist.gov/800-63-3/sp800-63b.html
- Konscious.Security.Cryptography.Argon2 NuGet v1.3.1: https://www.nuget.org/packages/Konscious.Security.Cryptography.Argon2
- Konscious.Security.Cryptography GitHub source: https://github.com/kmaragon/Konscious.Security.Cryptography
- ASP.NET Core Identity `PasswordHasher<TUser>` source (legacy V3 PBKDF2 fallback contract): https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Core/src/PasswordHasher.cs
- Cerebrum: `feedback_tier2_vendor_mock_first` — Tier-2 substrate discipline pattern (cross-tier reuse precedent at Tier-1 boundary)
- Cerebrum: `feedback_prefer_cleanest_long_term_option` — substrate-tier own-package decision rationale
- Cerebrum: `project_fleet_ruleset_config` — fleet ruleset posture (auto-merge fires on CI-green)
- Slotting-architecture three-tier taxonomy survey (the sibling shipyard slotting-architecture recommendation document) — Tier-1 domain-block discipline scoping for this substrate
