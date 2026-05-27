---
id: 97
title: PasswordHasher Substrate
status: Proposed
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

**Status:** Proposed (Revision 1; awaiting pre-merge dual-council attestation per ADR 0069 + H9. AMBER amendments will fold into Revision 2 per the ADR 0095 R2 / 0096 R2 / 0098 R2 precedent).
**Date:** 2026-05-27
**Resolves:** W#79 sub-cohort 1 H8 disposition (`coordination/inbox/admiral-ruling-2026-05-26T0035Z-w79-stage-05-9-halts-resolved.md` §H8) — pre-production hardening commitment that swaps the W#79 MVP-ship `IPasswordHasher<UserEntity>` PBKDF2-HMAC-SHA256 default for an OWASP-canonical Argon2id-backed implementation **before W#79 reaches production**. This ADR is the deferred-with-commitment. Codifies the substrate as a Tier-1 domain-block primitive with cross-tier reuse of ADR 0096's mock-first + production-guard discipline at the Tier-1 boundary. W#79 sub-cohort 1 ships at MVP with PBKDF2 V3 default; W#79 Step 2 (this ADR's Step 2) swaps the composition-root registration to Argon2id pre-production via a zero-handler-tier-change DI substitution.
**Council inputs:** Revision 1 forwards to dual-council. Sec-eng dual-council MANDATORY (per H9 + W#79 H8); .NET-architect dual-council MANDATORY (per H9). Promotion path: both councils self-attest GREEN via inbox status on Revision 1 → Admiral promotes ADR to `Accepted`. If a council returns AMBER, Admiral folds amendments into Revision 2 (ADR 0095 R2 / 0096 R2 / 0098 R2 precedent). **Step 1 (foundation-password-hashing substrate package) PR carries its own mandatory dual-council SPOT-CHECK at PR-open** per H9 — independent council pull on the cryptographic-primitive surface. **Step 2 (W#79 SignupHandler composition-root cutover) PR carries sec-eng MANDATORY SPOT-CHECK** for the substitution-correctness invariant.
**Predecessor research:** `shipyard/icm/01_discovery/research/onr-adr-0097-passwordhasher-substrate-scaffold.md` (1167 lines; ONR; merged via `shipyard#167`); ONR status `coordination/inbox/onr-status-2026-05-27T1428Z-adr-0097-passwordhasher-substrate-scaffold-complete.md`. Scaffold surfaced 11 halt conditions; Admiral 11-halt ruling (`coordination/inbox/admiral-ruling-2026-05-27T1430Z-adr-0097-passwordhasher-11-halts-resolved.md`) RATIFIED all 11 at ONR-recommended defaults in single pass (zero DEFERRED-CIC; zero OVERRIDE).

---

## Revision history

| Rev | Date | Author | Summary |
|---|---|---|---|
| 1 | 2026-05-27 | Admiral | Initial draft. Folds ONR scaffold (Option C — Argon2id via Konscious + retain `IPasswordHasher<TUser>` interface + version-tagged hash output for migration detection + Tier-1 domain-block placement) and Admiral 11-halt ruling (all 11 RATIFY-ONR-RECOMMENDATION). Codifies new `packages/foundation-password-hashing/` substrate package (Halt 3) carrying `Argon2idPasswordHasher<TUser>` + `Argon2idHashOptions` + `MockPasswordHasher<TUser>` + `IMockPasswordHasher` marker + `MockPasswordHasherProductionGuardAssertion` IHostedService + DI helpers `AddSunfishPasswordHashing<TUser>` + `AddSunfishMockPasswordHashing<TUser>`. Argon2id parameters pinned at OWASP minimum m=19456 KiB / t=2 / p=1 (Halt 1). NuGet library: `Konscious.Security.Cryptography.Argon2` v1.3.1 (Halt 2). Mock-first discipline applied at Tier-1 boundary via fresh `IMockPasswordHasher` marker family (Halt 4; distinct from ADR 0096's `IMockVendorProvider` — cross-tier reuse, not interface conflation). Hash output: Konscious PHC string format `$argon2id$v=19$m=...,t=...,p=...$<salt>$<hash>` (Halt 6). Migration: lazy rehash-on-next-login (Halt 5; bulk-rehash impossible by physics — no plaintext retrievable). No pepper at MVP; nullable `Argon2idHashOptions.Pepper` field reserved for future secret-store substrate (Halt 7). Pre-production sequencing: Steps 1-2 merge before W#79 reaches production-customer onboarding (Halt 8). Council cadence: sec-eng + .NET-architect dual-council MANDATORY on ADR text + Step 1; sec-eng MANDATORY SPOT-CHECK on Step 2 (Halt 9). Backward-compat: `Argon2idPasswordHasher.VerifyHashedPassword` delegates legacy V3 PBKDF2 byte[] format to a privately-composed `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` fallback; returns `PasswordVerificationResult.SuccessRehashNeeded` on legacy-hash verify success (Halt 10). Options binding: `IOptions<Argon2idHashOptions>` singleton snapshot — substrate-tier parameter changes ship via deployment, not runtime config reload (Halt 11). Status: Proposed (awaiting dual-council). |

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
