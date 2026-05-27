# ONR research — ADR 0097 PasswordHasher Substrate scaffold (W#79 H8 follow-on)

**Authored by:** ONR
**Requester:** Admiral (per `admiral-ruling-2026-05-26T0035Z-w79-stage-05-9-halts-resolved.md` H8 — RATIFY-ONR-WITH-FOLLOW-ON; ADR 0097 dispatched as pre-production-mandatory)
**Authored at:** 2026-05-27
**Type:** Research scaffold for new ADR 0097 (PasswordHasher Substrate)
**Status:** Draft for Admiral consumption — Admiral authors ADR 0097 Rev 1 text from this scaffold
**Trigger:** ADR 0098 Block-Naming Generalization Rev 1 merged 2026-05-27T00:28Z; substrate-ADR-Accept proactive-dispatch convention triggered ONR ADR 0097 scaffold authoring

---

## Scope of investigation

- **In scope:** Define the production-tier password-hashing substrate; audit the
  status-quo gap (W#79 sub-cohort 1 ships with `IPasswordHasher<UserEntity>` reuse
  on ASP.NET Identity's PBKDF2 V3 default; pre-production hardening commitment per
  H8); enumerate Argon2id substrate design options; recommend an option with
  reasoning; surface halt conditions Admiral must resolve before authoring ADR
  0097 Rev 1.
- **Out of scope:** Writing the ADR itself (Admiral territory per ADR 0095/0096/0098
  scaffold→ratification precedent); per-PR Stage-05 hand-off authoring (Step 1
  + Step 2 + optional Step 3 — downstream); the existing W#79 PBKDF2 V3 reuse
  (already locked at the H8 disposition; ADR 0097 follows W#79 sub-cohort 1 to
  production, NOT preempts it); password-policy concerns (minimum length, complexity,
  breach-pwd-list integration — separate concern; W#79 enforces "≥12 chars" floor
  per H7 ProblemDetails validation discriminator and is OUT of substrate scope);
  secret-rotation for the Argon2id pepper/secret (FORWARD-WATCH; secret-store
  substrate is separate); password-reset flow (W#80 sub-cohort territory).
- **Authoritative sources consulted:**
  - W#79 Stage-05 hand-off (`shipyard/.worktrees/onr-w79-stage-05-handoff/icm/05_implementation-plan/cohort-w79-hand-off.md`) — sec-eng A + .NET-arch K1/K2 amendments folded into Rev 2 establishing the `IPasswordHasher<UserEntity>` callsite-stable interface invariant + V3 versioned hash output for migration detection.
  - Admiral W#79 ruling H8 (`coordination/inbox/admiral-ruling-2026-05-26T0035Z-w79-stage-05-9-halts-resolved.md`) — ADR 0097 follow-on commitment; sec-eng SPOT-CHECK MANDATORY on each ADR + each Step PR; pre-production-mandatory gate.
  - ADR 0095 (Bootstrap Context substrate; `shipyard/docs/adrs/0095-bootstrap-context.md`) — substrate-tier ADR cadence precedent + `BootstrapAndTenantMutualExclusionAssertion` IHostedService startup-ordering precedent.
  - ADR 0096 (Tier-2 Vendor-Provider Substrate; `shipyard/docs/adrs/0096-tier-2-vendor-provider-substrate.md`) — mock-first discipline + `IMockVendorProvider` marker + `MockProviderProductionGuardAssertion` IHostedService + compile-time generic-constraint mechanism. **Critical shape question** below (§3.7): does PasswordHasher warrant Tier-2 substrate discipline, or is it Tier-1 domain-block with no vendor swap?
  - ADR 0098 (Block-Naming Generalization; `shipyard/docs/adrs/0098-block-naming-generalization.md`) — substrate-tier ADR Rev 2 cadence + dual-council MANDATORY precedent.
  - ADR 0013 (Foundation.Integrations; provider-neutrality precedent) — HttpClient-only adapter convention; the Argon2id `Konscious.Security.Cryptography.Argon2` NuGet is **not a network vendor adapter** so ADR 0013 supply-chain discipline applies in spirit but not letter.
  - ADR 0091 R2 (`TenantContextRegistrationAssertion` IHostedService) — "POSITIVE invariant provable at startup via static descriptor inspection" precedent.
  - OWASP Password Storage Cheat Sheet (retrieved 2026-05-27; canonical Argon2id parameter floor: m=19 MiB / t=2 / p=1 minimum; m=46 MiB / t=1 / p=1 alternative).
  - `Konscious.Security.Cryptography.Argon2` NuGet package (v1.3.1; Argon2 1.3 spec; MIT licensed; documented in OWASP cheatsheet as the canonical .NET Argon2id implementation).
  - ASP.NET Core Identity `PasswordHasher<TUser>` source (`PasswordHasherCompatibilityMode.IdentityV3` = PBKDF2-HMAC-SHA256, 100k iterations, version byte 0x01 in output; `PasswordVerificationResult.SuccessRehashNeeded` is the canonical migration-trigger mechanism).
  - Repository scan: `IPasswordHasher` consumer count in shipyard packages = **zero** (verified via `grep -rn "IPasswordHasher" packages/ --include="*.cs"`); the only forthcoming consumer is the W#79 signup handler in `signal-bridge/Sunfish.Bridge.Onboarding`.
- **Success criteria:** Admiral can author ADR 0097 Rev 1 from this scaffold
  without re-discovering the status-quo audit, the Argon2id-vs-alternatives
  analysis, the migration-detection mechanism, or the Tier-1-vs-Tier-2 cluster
  question. All decisions inside the ADR's scope have a recommended position;
  everything outside the ADR's scope has a halt condition naming who must resolve
  it. Halt count is in the expected range (8–11 per ADR 0095/0096/0098
  scaffold-density precedent).

---

## TL;DR

- **Problem.** ASP.NET Core Identity's default `IPasswordHasher<TUser>` is
  PBKDF2-HMAC-SHA256 at 100k iterations (`PasswordHasherCompatibilityMode.IdentityV3`).
  This is **acceptable for MVP** but **not acceptable for production** per the H8
  ruling: OWASP's canonical password-storage cheatsheet (retrieved 2026-05-27)
  names Argon2id as the default choice for general-purpose applications, with
  minimum parameters m=19 MiB / t=2 / p=1. The industry shift from PBKDF2 to
  Argon2id is settled — every modern guidance source (OWASP, NIST SP 800-63B-3,
  IETF RFC 9106) names Argon2id as the preferred memory-hard KDF for new
  systems. PBKDF2 remains FIPS-validated and is the correct compromise for
  regulated environments that require FIPS, but Sunfish is not currently in a
  FIPS-mandate posture.
- **What ADR 0097 must define.** A small, **production-mandatory** PasswordHasher
  substrate that (a) replaces the W#79 PBKDF2 V3 default with Argon2id-by-default
  in production; (b) defines a version-tagged hash output format that lets
  `IPasswordHasher<TUser>.VerifyHashedPassword(...)` return
  `PasswordVerificationResult.SuccessRehashNeeded` when an old PBKDF2 hash is
  observed on login (the migration trigger); (c) introduces a `MockPasswordHasher`
  for dev/test with the ADR 0096 mock-first discipline applied at Tier-1 boundary
  (justification below — Tier-1 normally doesn't run mock-first, but the
  fast-test-loop value plus the sec-eng A "interface invariant" already in place
  make this the cheapest discipline-application win in the substrate).
- **Recommended option.** **Option C — `IPasswordHasher<TUser>` retention +
  Argon2id-backed concrete substitution + version-tagged hash output for migration
  detection + Tier-1 domain-block placement (NOT Tier-2 vendor swap).** Mirrors
  W#79 Rev 2 sec-eng A + .NET-arch K1/K2 invariants (handler-tier callsite stable;
  V3-versioned-output is the migration trigger); avoids overloading the substrate
  with vendor-swap concerns it does not need (Argon2id is a cryptographic primitive
  with one canonical algorithm, not a vendor surface).
- **Halt conditions for Admiral.** (1) Argon2id parameters (memory/time/parallelism)
  — OWASP minimum m=19 MiB / t=2 / p=1 vs alternative m=46 MiB / t=1 / p=1 vs
  security-conscious m=64 MiB / t=3 / p=1 (per industry hardening guidance);
  (2) NuGet library choice (Konscious.Security.Cryptography.Argon2 v1.3.1 vs
  alternative implementations); (3) Tier-1 domain-block vs Tier-2 vendor-substrate
  cluster home (recommended Tier-1; halt-confirm); (4) Mock-first discipline
  application at Tier-1 (recommended yes for cheap fast-test-loop win; halt-confirm);
  (5) Migration strategy — login-time rehash only vs background-job bulk-rehash
  vs hybrid; (6) Hash output format — Konscious's default `$argon2id$v=19$m=...$...`
  string vs ASP.NET Identity's V3 `byte[]` with version byte vs custom; (7) Pepper
  / static secret augmentation (server-side secret XOR'd into the hash material,
  per OWASP optional hardening); (8) Pre-production gate sequencing (before W#79
  reaches MVP launch vs before W#79 reaches production-customer onboarding —
  semantic difference if MVP-launch and production-customer-onboarding are
  separate milestones); (9) Sec-eng dual-council MANDATORY on the substrate ADR
  + each Step PR (confirmed in H8; halt-confirm); (10) Backward-compat read-side
  discipline for the (currently-zero) existing PBKDF2 hashes after W#79 ships
  but before ADR 0097 lands (migration period is well-defined: zero existing
  hashes pre-W#79; small but growing set during MVP → ADR-0097-prod-cutover; the
  bulk-rehash question only matters if the cutover is delayed past the first
  small wave of paying customers).

---

## 1. Problem statement

### 1.1 What is the PasswordHasher Substrate?

The "PasswordHasher Substrate" is the typed scoped DI primitive
(`IPasswordHasher<TUser>`) that Sunfish's signup, login, and password-reset
handlers inject to (a) hash a plaintext password for storage at registration
time and (b) verify a candidate plaintext against a stored hash at authentication
time. For 100% of Sunfish today, that substrate is empty — there are zero
existing consumers (`grep -rn "IPasswordHasher" packages/ --include="*.cs"`
returns no hits in the shipyard tree, verified 2026-05-27).

W#79 sub-cohort 1 is the first surface area that introduces a non-trivial
password-hashing dependency:

| Surface (per W#79 hand-off Rev 2) | When PasswordHasher runs | Algorithm at W#79 ship |
|---|---|---|
| `POST /api/signup` → `SignupHandler.HandleAsync(...)` | `STEP 2b` — unconditional `passwordHasher.HashPassword(stubUser, request.Password)` (sec-eng D constant-work discipline) | ASP.NET Identity `PasswordHasher<UserEntity>` default — PBKDF2-HMAC-SHA256, 100k iterations, version byte 0x01 |
| `GET /api/signup/verify-email/{signed-token}` | Reads the stored hash from `User.PasswordHash`; no hash operation in this path | Same |
| `POST /api/auth/login` (W#80 sub-cohort) | `passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)` | Same |

ADR 0097 swaps the concrete `IPasswordHasher<UserEntity>` implementation from
ASP.NET Identity's PBKDF2 V3 default to an Argon2id-backed implementation. The
handler-tier callsite signature (`IPasswordHasher<UserEntity>` ctor injection;
`HashPassword(user, password)` + `VerifyHashedPassword(user, hash, password)`)
is preserved — the substrate change is **DI-registration-only**. Per W#79 Rev 2
sec-eng A + .NET-arch K1, this is the codified invariant: ADR 0097 must NOT
introduce callsite changes to the W#79 signup handler.

### 1.2 Why this needs an ADR (vs an inline implementation note)

Four reasons make this substrate-tier and worth Admiral-authority ratification:

1. **Production-safety is load-bearing.** Argon2id-vs-PBKDF2 is not a stylistic
   choice; it is a regulatory + reputational floor. NIST SP 800-63B-3 (Section
   5.1.1.2) names PBKDF2 with "at least 10,000 iterations" as the floor for
   memorized-secret verifiers; ASP.NET Identity's 100k is 10× the floor and is
   PBKDF2-conformant. OWASP names Argon2id as the **default choice for general-
   purpose applications** and recommends the migration path PBKDF2 → Argon2id.
   The W#79 H8 disposition explicitly defers the hardening to ADR 0097 with the
   gate "MUST land before W#79 reaches production." This ADR is the deferred
   commitment.

2. **The interface shape constrains all future authentication paths.** W#79
   (signup), W#80 (login + password reset; downstream), and future SSO-fallback
   paths consume `IPasswordHasher<UserEntity>`. Choosing the wrong shape — e.g.,
   introducing a `IPasswordHasher` (non-generic) wrapper that breaks the W#79
   sec-eng A invariant — propagates across the entire authentication ladder.
   The ADR captures the algorithm-swap mechanism once; the migration-detection
   mechanism (V3-versioned-output → `SuccessRehashNeeded`) is referenced from
   the W#80 login-handler hand-off.

3. **Tier-1 vs Tier-2 cluster home is a substrate-discipline call.** PasswordHasher
   is **not** a vendor surface — Argon2id is a cryptographic primitive with one
   canonical algorithm (RFC 9106); there is no "vendor swap" the way Postmark
   vs Mailgun is a vendor swap for `IEmailProvider`. But the W#79 Rev 2 sec-eng
   A invariant ("interface, NEVER static") plus the dev/test fast-loop value of
   a `MockPasswordHasher` (no-op hash; constant-time return; bypasses Argon2id's
   tens-of-ms cost in unit tests where the hash itself is not under test) makes
   a Tier-2-discipline-flavored shape compelling at Tier-1 boundary. Whether
   that's the right call is a Halt — see §3.7.

4. **The cryptography-relevance dual-council attest is non-trivial.** Algorithm
   choice (Argon2id), parameter selection (m=19 MiB / t=2 / p=1 vs m=46 MiB /
   t=1 / p=1 vs m=64 MiB / t=3 / p=1), hash-output format (Konscious string vs
   ASP.NET V3 byte[] vs custom), and migration-detection mechanism (version
   byte vs format-string prefix) are all decisions where sec-eng-council and
   the .NET-architect council have asymmetric expertise. ADR-tier ratification
   with sec-eng MANDATORY (per H8) is the correct cadence.

### 1.3 What ADR 0097 must NOT define

To stay scoped, ADR 0097 must explicitly **not** define:

- The W#79 signup handler implementation shape (it is locked at the W#79 Stage-05
  hand-off Rev 2; ADR 0097 inherits sec-eng A + .NET-arch K1/K2 + sec-eng D
  constant-work invariants and MUST preserve them).
- Password-policy concerns (minimum length, complexity rules, breach-pwd-list
  integration via HaveIBeenPwned) — these are application-tier concerns; W#79
  enforces "≥12 chars" floor at ProblemDetails validation, and substrate has
  no opinion on plaintext content.
- Password-reset flow (W#80 sub-cohort territory; consumes this substrate but
  does not shape it).
- Secret-rotation for the optional Argon2id pepper / static-secret (FORWARD-WATCH;
  secret-store substrate is separate from password-hash substrate).
- The login-handler shape itself (W#80; consumes the
  `PasswordVerificationResult.SuccessRehashNeeded` migration-trigger but does not
  shape it).
- The HTTP API surface (no new endpoints; substrate-only).
- The audit-event types emitted when a hash is upgraded (ADR 0049 enum extension
  is a future concern; recommended audit-event `user_password_hash_upgraded`
  noted as forward-watch).

These all consume the PasswordHasher Substrate or are adjacent application-tier
concerns; none of them belong in the substrate ADR.

---

## 2. Status-quo audit

### 2.1 PasswordHasher-adjacent code that exists today

Findings from a targeted scan of shipyard packages + signal-bridge + sunfish:

| Symbol / path | Existing? | Role in password-hashing window | Reusable as-is? |
|---|---|---|---|
| `Microsoft.AspNetCore.Identity.IPasswordHasher<TUser>` | yes (BCL / Microsoft.AspNetCore.Identity.Core) | The canonical typed interface — `HashPassword(TUser, string)` + `VerifyHashedPassword(TUser, string, string)` returning `PasswordVerificationResult` | **Yes — interface preserved per sec-eng A + .NET-arch K1** |
| `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` (default impl) | yes | Default impl; `PasswordHasherCompatibilityMode.IdentityV3` → PBKDF2-HMAC-SHA256, 100k iter, version byte 0x01 | No — production-tier replacement is the substrate change |
| `Microsoft.AspNetCore.Identity.PasswordVerificationResult` (enum) | yes | `Failed` / `Success` / `SuccessRehashNeeded` — the third value IS the migration-trigger primitive | **Yes — load-bearing for ADR 0097 migration mechanism** |
| `Microsoft.AspNetCore.Identity.PasswordHasherOptions.CompatibilityMode` (enum) | yes | `IdentityV2` (PBKDF2-SHA1) / `IdentityV3` (PBKDF2-SHA256) — ASP.NET Identity has no Argon2id mode | No — proves Sunfish must ship its own Argon2id-backed `IPasswordHasher<TUser>` impl; cannot just toggle a CompatibilityMode enum |
| `Konscious.Security.Cryptography.Argon2` NuGet (v1.3.1) | available — not yet referenced | C# implementation of the Argon2 1.3 spec with Argon2i/Argon2d/Argon2id variants; MIT licensed; OWASP-cheatsheet recommended for .NET | **Yes — recommended cryptographic primitive** |
| `Sunfish.Foundation.Authorization.ICurrentUser` (ADR 0091 R2) | yes | Caller identity surface | Irrelevant to hash substrate (PasswordHasher operates before ICurrentUser is populated) |
| `Sunfish.Foundation.Bootstrap.IBootstrapContext` (ADR 0095) | yes (merged 2026-05-26 via shipyard#158) | Pre-tenant DI primitive; SignupHandler resolves this scope | Irrelevant — PasswordHasher runs WITHIN the bootstrap scope but is bootstrap-context-agnostic (the hash doesn't depend on tenant identity) |
| `Sunfish.Foundation.Integrations.IMockVendorProvider` (ADR 0096) | yes (merged 2026-05-26 via shipyard#159) | Marker interface for mock implementations; powers `MockProviderProductionGuardAssertion` startup invariant | **Pattern to mirror IF Halt 4 ratifies mock-first at Tier-1 boundary** |
| `Sunfish.Foundation.Integrations.DependencyInjection.MockProviderProductionGuardAssertion` | yes (merged 2026-05-26) | IHostedService startup invariant — descriptor scan for mock concretes + production-safety predicate | **Pattern to mirror IF Halt 4 ratifies mock-first at Tier-1 boundary** |
| `Sunfish.Foundation.Integrations.DependencyInjection.VendorProviderServiceCollectionExtensions.AddSunfishVendorProvider<TContract, TConcrete>` | yes (merged 2026-05-26) | Compile-time mock-marker constraint `where TConcrete : class, TContract, IMockVendorProvider` | **Pattern to mirror IF Halt 4 ratifies** — Option C in §3 derives a `AddSunfishPasswordHasher<TUser>(...)` extension with similar compile-time discipline if Halt 4 ratifies |
| W#79 `SignupHandler.HandleAsync(...)` ctor | not yet merged (pending W#79 Stage-05 PR opening post-Step-2-merge) | Injects `IPasswordHasher<UserEntity>` per sec-eng A + .NET-arch K1 | **Yes — handler is non-load-bearing on algorithm choice; ADR 0097 swap requires zero callsite changes** |
| `Sunfish.Bridge.Onboarding` project | not yet merged (W#79 PR 0 substrate; pending) | The new cluster home for signup/login/verify-email handlers | Relevant — `IPasswordHasher<UserEntity>` registration belongs in this project's composition-root branch OR in a new shipyard package (see §3 cluster-home options) |

### 2.2 The four hard gaps

1. **No `IPasswordHasher<TUser>` registration exists in any composition root today.**
   W#79 sub-cohort 1 introduces the first registration (default ASP.NET Identity
   PBKDF2 V3). ADR 0097 must define WHERE the registration lives (Halt 3
   cluster-home) and WHEN the Argon2id-backed concrete swaps in (Halt 8 sequencing).

2. **ASP.NET Identity's `PasswordHasherOptions.CompatibilityMode` has no
   Argon2id mode.** A direct configuration knob would have been the cleanest
   path; absent that, Sunfish must ship its own `Argon2idPasswordHasher<TUser> :
   IPasswordHasher<TUser>` concrete. This is the unavoidable substrate-tier cost.

3. **Migration detection requires a versioning convention in the hash output.**
   ASP.NET Identity's V3 byte[] format includes a version byte (0x01 for V3
   PBKDF2-HMAC-SHA256); Konscious.Argon2's default string format begins with
   `$argon2id$v=19$m=...$t=...$p=...$<salt>$<hash>` and is self-describing.
   Two compatible formats; ADR 0097 must pin which one is canonical at Sunfish-
   tier and how the `Argon2idPasswordHasher<TUser>.VerifyHashedPassword(...)`
   implementation parses BOTH formats on input (read-side back-compat) but
   writes only the Sunfish-canonical format on output. This is Halt 6.

4. **The W#79 sec-eng D constant-work discipline assumes PBKDF2 timing.** The
   W#79 hand-off comment line "tens of ms" cost is calibrated to PBKDF2 100k.
   Argon2id at OWASP-minimum (m=19 MiB / t=2 / p=1) has comparable wall-clock
   cost (~50-100 ms on a modern x86 server), but at security-conscious
   parameters (m=64 MiB / t=3 / p=1) the cost rises to ~250-500 ms. The W#79
   "±25% p50/p95 latency parity floor" survives the swap at OWASP-minimum
   parameters but may not survive at security-conscious parameters. This is a
   FORWARD-WATCH to W#79 sec-eng SPOT-CHECK author (already MANDATORY per H8 +
   already promised by the W#79 ruling): the integration test threshold may
   need re-tuning post-ADR-0097-cutover.

### 2.3 What about ADR 0095 / 0096 / 0098 substrate ADRs?

Each of these three ADRs is the structurally-closest precedent for ADR 0097:

- **ADR 0095 (Bootstrap Context)** — substrate-tier ADR for a pre-tenant DI
  primitive; same Rev 1 → Rev 2 dual-council fold cadence; same R2-A1
  registration-snapshot-invariant pattern. ADR 0097 SHAPE MIRRORS this for
  algorithm-swap-mechanism prose.
- **ADR 0096 (Tier-2 Vendor-Provider Substrate)** — substrate-tier ADR for
  mock-first vendor swap; load-bearing `IMockVendorProvider` + `MockProviderProductionGuardAssertion`
  + compile-time marker constraint. ADR 0097 SHAPE MIRRORS this for Halt 4
  (mock-first-at-Tier-1) if ratified, otherwise inherits the substrate-tier-ADR
  text density without the marker/production-guard machinery.
- **ADR 0098 (Block-Naming Generalization)** — substrate-tier ADR for cross-vertical
  rename; same Rev 1 → Rev 2 dual-AMBER → 17-amendment fold cadence (per the
  Revision history). ADR 0097 cadence is identical: scaffold (this) → Admiral
  ruling on halts → Admiral Rev 1 ADR text → dual-council (sec-eng + .NET-arch)
  → AMBER fold → Rev 2 → re-attest → Accepted.

### 2.4 Industry / standards landscape

Primary sources consulted 2026-05-27:

| Source | Date | Tier | Position on Argon2id |
|---|---|---|---|
| OWASP Password Storage Cheat Sheet | retrieved 2026-05-27 | Primary (industry consensus authority) | Argon2id is the **default choice**; minimum m=19 MiB / t=2 / p=1; alternative m=46 MiB / t=1 / p=1 |
| NIST SP 800-63B-3 Section 5.1.1.2 | 2017-06 (still current as of 2026-05-27) | Primary (US federal standard) | PBKDF2 with ≥10k iter is the floor for memorized-secret verifiers; does NOT name Argon2id (predates RFC 9106); FIPS-validation constraint may pin PBKDF2 for federal regulated environments |
| IETF RFC 9106 (Argon2 Memory-Hard Function) | 2021-09 | Primary (protocol standard) | Argon2id is the **recommended variant** for general password hashing; standardizes the algorithm + parameters |
| OWASP Application Security Verification Standard 4.0.3 §2.4 | 2024-10 | Primary (verification framework) | Argon2id with parameters per OWASP cheatsheet; PBKDF2 + bcrypt + scrypt acceptable alternatives |
| ASP.NET Core Identity `PasswordHasher<TUser>` source | dotnet/aspnetcore main branch, retrieved 2026-05-27 | Primary (implementation precedent) | PBKDF2-HMAC-SHA256 (V3) is the framework default; `SuccessRehashNeeded` is the canonical migration trigger |
| Konscious.Security.Cryptography.Argon2 NuGet v1.3.1 | published 2024-11-05; v1.3 spec | Primary (canonical .NET impl) | The OWASP-cheatsheet-recommended .NET Argon2 library; MIT licensed; Argon2id + Argon2d + Argon2i variants |
| "Password Hashing Algorithms Compared" (BellatorCyber) | 2023 (retrieved 2026-05-27) | Tertiary (industry commentary) | Argon2id wins for new systems; PBKDF2 + bcrypt acceptable for legacy + FIPS-mandate |
| diegofercri/password-hasher (GitHub, C# impl) | 2024 (retrieved 2026-05-27) | Tertiary (open-source reference impl) | Performance benchmarks for Konscious + parameter-selection guidance |
| Twelve21 "How to Use Argon2 for Password Hashing in C#" | 2022 (retrieved 2026-05-27) | Tertiary (industry commentary; Konscious primary author writes this) | Worked example of Konscious; parameter selection rationale |

**Synthesis.** The industry consensus on Argon2id for new systems is settled
(2017 password-hashing-competition winner; 2021 IETF standardization; 2024
OWASP cheatsheet primary recommendation; 2024 OWASP ASVS Level-1/2/3 inclusion).
The .NET ecosystem has a single canonical implementation
(`Konscious.Security.Cryptography.Argon2`); no major competing implementation
(diegofercri's impl is a reference + benchmarking project, not production-tier
canonical). FIPS-validation is the **only** scenario where PBKDF2 retention is
the correct call; Sunfish is not in a FIPS-mandate posture (verified — no
FIPS-compliance concern in ADR 0046 production-readiness checklist).

---

## 3. Options analysis

Six candidate designs for the PasswordHasher Substrate, ordered roughly by
increasing invasiveness + cleanest-long-term progression:

### 3.1 Option A — Keep PBKDF2 V3; raise iteration count

**Shape.** Keep ASP.NET Identity's `PasswordHasher<TUser>` default; bump
`PasswordHasherOptions.IterationCount` from 100k to 600k (the 2026-OWASP-floor
for PBKDF2-HMAC-SHA256 per the cheatsheet's PBKDF2 section).

**Pros.**
- Zero new dependencies.
- Zero new code.
- FIPS-validated (PBKDF2-HMAC-SHA256 is FIPS-approved).

**Cons.**
- **Rejected by OWASP for new systems.** OWASP cheatsheet (2026): "Argon2id is
  the default choice; if Argon2id is not available, bcrypt is the fallback;
  PBKDF2 is acceptable only for FIPS-mandated environments." Sunfish is not
  FIPS-mandated.
- PBKDF2 is **not memory-hard**. Modern GPU + ASIC attacks against PBKDF2 at
  600k iterations remain economically feasible at scale; Argon2id's memory-hard
  property is the load-bearing defense against this.
- Misses the W#79 H8 commitment ("Argon2id per OWASP"). Reusing PBKDF2 with
  higher iter count does not satisfy the ruling.

**Recommendation.** **Reject.** Misses the H8 commitment and the industry shift.

### 3.2 Option B — Migrate to bcrypt

**Shape.** Replace ASP.NET Identity's PBKDF2 V3 with bcrypt via the
`BCrypt.Net-Next` NuGet (the canonical .NET bcrypt library).

**Pros.**
- Mature library (in production since ~2002).
- Memory-hard property (more than PBKDF2; less than Argon2id).
- Widely deployed; familiar to most .NET teams.

**Cons.**
- **Argon2id supersedes bcrypt** per OWASP for new systems; bcrypt is the
  cheatsheet's "fallback when Argon2id unavailable" tier.
- bcrypt has a 72-byte password length cap (or null-byte truncation,
  depending on implementation). Sunfish's "≥12 chars" minimum has no cap-
  related concern in practice, but the discipline of "use the recommended
  primary algorithm" matters at substrate tier.
- The memory-hard property of bcrypt is weaker than Argon2id's tunable memory
  parameter.

**Recommendation.** **Reject.** Not the OWASP primary recommendation; no
compensating advantage over Argon2id.

### 3.3 Option C — Argon2id via Konscious + retain `IPasswordHasher<TUser>` interface (RECOMMENDED)

**Shape.** Add `Konscious.Security.Cryptography.Argon2` NuGet package reference;
implement a new `Sunfish.Foundation.PasswordHashing.Argon2idPasswordHasher<TUser> :
IPasswordHasher<TUser>` concrete that:
1. `HashPassword(TUser user, string password)`:
   - Generates 16-byte cryptographically random salt via `RandomNumberGenerator.GetBytes(16)`.
   - Instantiates `Argon2id(Encoding.UTF8.GetBytes(password))` with parameters
     pinned at substrate tier (see Halt 1).
   - Sets `Salt`, `Iterations` (= t), `MemorySize` (= m KiB), `DegreeOfParallelism` (= p).
   - Computes 32-byte hash via `GetBytesAsync(32)`.
   - Returns the Konscious-canonical string format
     `$argon2id$v=19$m=19456,t=2,p=1$<base64-salt>$<base64-hash>` (OWASP-canonical
     wire format; self-describing for migration).
2. `VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)`:
   - Inspects `hashedPassword` prefix:
     - If begins with `$argon2id$`: parses Konscious format; verifies; returns `Success` or `Failed`.
       Optionally returns `SuccessRehashNeeded` if the embedded `m=` / `t=` / `p=`
       parameters are below the current substrate-tier floor (parameter-floor
       upgrade mechanism for future hardening).
     - Else (legacy ASP.NET Identity V3 byte[] format; recognizable by Base64
       decode → first byte 0x01): delegates to a private
       `_legacyHasher : Microsoft.AspNetCore.Identity.PasswordHasher<TUser>`
       composed at construction; if verification succeeds returns
       `PasswordVerificationResult.SuccessRehashNeeded` (the migration trigger);
       if verification fails returns `Failed`.
3. DI registration via new `Sunfish.Foundation.PasswordHashing.DependencyInjection.AddSunfishPasswordHashing<TUser>()`
   extension method that:
   - Registers `Argon2idPasswordHasher<TUser>` as the `IPasswordHasher<TUser>` concrete (singleton).
   - Registers `PasswordHasher<TUser>` as a private dependency for legacy-hash fallback verification.
   - Optionally registers `MockPasswordHasher<TUser>` IF Halt 4 ratifies mock-first.

**Pros.**
- **OWASP-canonical algorithm + library.** Matches H8 commitment exactly.
- **Preserves `IPasswordHasher<TUser>` interface.** Zero W#79 handler callsite changes.
- **V3-versioned-byte read-side back-compat + Argon2id wire-format write-side
  forward-compat.** Old PBKDF2 hashes verify on first login and trigger
  `SuccessRehashNeeded`; the W#80 login handler rehashes + writes Argon2id
  format; eventually all stored hashes are Argon2id-canonical.
- **Self-describing wire format.** The `$argon2id$v=19$m=...$t=...$p=...$...$...`
  string format records the parameters that were used at hash time. Future
  parameter hardening (m=19 → m=46 → m=64) can detect "this hash was made with
  weaker parameters than the current floor" and return `SuccessRehashNeeded`
  on next login — automatic parameter-floor upgrade.
- **No vendor surface to swap.** Argon2id has a single canonical algorithm
  (RFC 9106); future hardening is a parameter knob, not a vendor change.

**Cons.**
- New external NuGet dependency (`Konscious.Security.Cryptography.Argon2`).
  Supply-chain risk mitigated by: MIT licensed, OWASP-cheatsheet-recommended,
  Argon2-1.3-spec-conformant, audited via dotnet/nuget verification, no
  transitive runtime dependencies beyond BCL.
- Tens-to-hundreds-of-ms hash cost (depending on parameters). Tunable but not
  zero. The W#79 sec-eng D constant-work discipline already absorbs the cost
  unconditionally per handler-tier invariant; Argon2id parameter choice
  affects ABSOLUTE latency but not the constant-work property.
- Cluster-home decision (Halt 3) is non-trivial — see §3.7.
- Pepper / static-secret augmentation is optional and not in the recommended
  baseline (Halt 7).

**Recommendation.** **RECOMMENDED.** This is the canonical OWASP-conformant
shape; matches the W#79 H8 commitment; preserves the W#79 sec-eng A + .NET-arch
K1 interface invariant; provides a clean version-tagged migration mechanism.

### 3.4 Option D — Argon2id via Konscious + introduce new non-generic `IPasswordHasher` wrapper

**Shape.** Same as Option C, plus a new non-generic
`Sunfish.Foundation.PasswordHashing.IPasswordHasher` abstraction layer that
hides ASP.NET Identity's typed `IPasswordHasher<TUser>` behind a Sunfish-owned
domain-tier interface. The new `IPasswordHasher` is what the SignupHandler
injects; an adapter binds `IPasswordHasher<TUser>` (which is what ASP.NET
Identity's other moving parts may still need) to the same underlying concrete.

**Pros.**
- Decouples Sunfish from `Microsoft.AspNetCore.Identity.Core`'s typed contract.
  (Speculative future: if Sunfish ever drops ASP.NET Identity entirely.)

**Cons.**
- **Violates W#79 sec-eng A invariant.** The W#79 Rev 2 fold explicitly pins
  `IPasswordHasher<UserEntity>` (generic) as the injection point with the
  rationale "NEVER static; ADR 0097 future Argon2id swap requires zero
  callsite changes." Option D introduces a callsite change.
- "Decouple from ASP.NET Identity" is speculative; ASP.NET Identity is
  Sunfish's identity substrate per ADR 0091 R2's `ICurrentUser` integration
  + `UserEntity : IdentityUser<Guid>` per W#79's `Sunfish.Bridge.Onboarding.UserEntity`.
- Adds a layer with no current consumer beyond SignupHandler.

**Recommendation.** **Reject.** Violates W#79 sec-eng A invariant; speculative
future-proofing.

### 3.5 Option E — Argon2id via Konscious + apply ADR 0096 Tier-2 vendor-substrate discipline

**Shape.** Same as Option C, plus apply the full ADR 0096 Tier-2
Vendor-Provider Substrate pattern: `Argon2idPasswordHasher<TUser>` is the real
adapter (registered via `UseVendorProviderIfConfigured`); `MockPasswordHasher<TUser> :
IPasswordHasher<TUser>, IMockVendorProvider` is the mock (registered via
`AddSunfishVendorProvider`); `MockProviderProductionGuardAssertion` detects
mock-in-production at startup.

**Pros.**
- Fully discipline-conformant with ADR 0096's mock-first substrate pattern.
- Compile-time `IMockVendorProvider` constraint catches "mock concrete forgot
  the marker" at compile time.

**Cons.**
- **Conceptually wrong.** Argon2id is NOT a vendor surface. There is no
  Postmark-vs-Mailgun-equivalent decision; there is one canonical algorithm
  (RFC 9106 Argon2id) and one canonical .NET library (Konscious). The
  Tier-2 vendor-substrate pattern presumes a swap surface that doesn't exist.
- The `IMockVendorEnvVarRegistry` mapping presumes the real adapter is
  gated on a vendor-API-key env var. Argon2id has no such gate — the
  parameters m/t/p are not "credentials" in the same sense.
- The mock-in-production assertion adds load-bearing infrastructure for a
  property (algorithm choice) that is more directly expressed via the existing
  ASP.NET-environment check (`ASPNETCORE_ENVIRONMENT != "Production"` plus
  DI registration shape).
- ADR 0096 itself documents the discipline as **Tier-2 vendor-substrate**;
  conflating it with Tier-1 domain-block primitives weakens the framing.

**Recommendation.** **Reject as Tier-2 pattern.** BUT the discipline of "mock
impl carries a marker; production-safety asserts at startup" is genuinely
valuable at Tier-1 too. See Option F.

### 3.6 Option F — Argon2id via Konscious + Tier-1 mock-first discipline (cheaper Option E)

**Shape.** Same as Option C, plus:
- `MockPasswordHasher<TUser> : IPasswordHasher<TUser>` — dev/test no-op hash;
  returns a deterministic string `mock-hash-of-{Encoding.UTF8.GetByteCount(password)}`
  (NOT the password itself; that's a verify-side enumeration risk).
- `MockPasswordHasher` carries an `IMockPasswordHasher` marker (new; analogous
  to but DISTINCT from `IMockVendorProvider`).
- A new `MockPasswordHasherProductionGuardAssertion : IHostedService` runs at
  startup; descriptor-scans `IPasswordHasher<>` registrations; if any concrete
  implements `IMockPasswordHasher`, requires `ASPNETCORE_ENVIRONMENT !=
  "Production"` OR `SUNFISH_ALLOW_MOCK_PASSWORD_HASHER=true` opt-out env var.
- Compile-time generic-constraint on `AddSunfishPasswordHashing<TUser, TConcrete>()`
  IF the registration helper is parameterized (recommended NO — see §3.6.3 below).

**3.6.1 Pros.**
- Cheap fast-test-loop win: unit tests that exercise the SignupHandler can
  use the mock and skip the Argon2id tens-of-ms cost (test-suite total cost
  matters at fleet scale).
- Production-safety invariant analogous to ADR 0096's pattern, without the
  vendor-substrate framing.
- The "mock production guard" pattern is genuinely valuable at any tier where
  a dev/test substitute is registered — Tier-1 is not excluded from this
  benefit by definition.

**3.6.2 Cons.**
- Adds ~80-100 LOC for the marker + production-guard IHostedService that
  Tier-1 domain-blocks normally do not carry. Tier-2 substrate carries this
  weight to enable real-vendor swap-out; Tier-1 carries it only to enable
  fast-test-loop, which is a smaller motivation.
- The `IMockPasswordHasher` marker is a distinct interface from
  `IMockVendorProvider` (and conflating them would be wrong — see §3.5 Cons).
  The substrate gains a "mock-marker family" rather than a single canonical
  `IMockProvider` marker; halt-confirm whether this duplication is acceptable
  vs preferable.

**3.6.3 Sub-design — DI helper shape if Halt 4 ratifies.**
The cleanest `AddSunfishPasswordHashing<TUser>()` overloads:
- `AddSunfishPasswordHashing<TUser>(this IServiceCollection)`:
  registers `Argon2idPasswordHasher<TUser>` as the singleton real impl.
- `AddSunfishMockPasswordHashing<TUser>(this IServiceCollection)`:
  registers `MockPasswordHasher<TUser>` as the singleton mock impl + registers
  the `MockPasswordHasherProductionGuardAssertion` IHostedService.

This shape is parallel to ADR 0096's `AddSunfishVendorProvider` /
`UseVendorProviderIfConfigured` split, but tuned for Tier-1's simpler
"either-real-or-mock; no conditional-env-var swap" model.

**Recommendation.** **RECOMMENDED IF Halt 4 ratifies mock-first at Tier-1**;
otherwise Option C is the floor and the mock-marker/production-guard
machinery is elided.

### 3.7 Halt-question: Tier-1 vs Tier-2 cluster home

This is the load-bearing structural question for the ADR. Three sub-options:

- **Sub-option α — Tier-1 domain-block in NEW `packages/foundation-password-hashing/`.**
  - Argument FOR: PasswordHasher is a cryptographic primitive that operates
    over (TUser, string) → (string), with no vendor surface and no tenant-keying.
    It is conceptually parallel to `Sunfish.Foundation.Authorization.ICurrentUser`
    (Tier-1 identity primitive) or `Sunfish.Foundation.Time.IClock` (Tier-1
    time primitive) — substrate-level, but not vendor-substrate.
  - Argument AGAINST: A whole new package for a single interface + concrete +
    optional mock is overhead. Could co-locate in `foundation-authorization`
    (the substrate that already hosts `ICurrentUser` + `ITenantContext`) per
    "auth substrate cluster."
- **Sub-option β — Tier-1 domain-block co-located in `packages/foundation-authorization/`.**
  - Argument FOR: Cheaper than a new package; semantically related (authn
    cluster).
  - Argument AGAINST: `foundation-authorization`'s job per ADR 0091 R2 is
    *authorization-context resolution* — tenant + caller + policy. Hash
    primitives are *authentication-credential-storage*, conceptually upstream
    of authorization. Bundling them confuses the substrate's purpose.
- **Sub-option γ — Tier-2-discipline-flavored in `packages/foundation-integrations/`
  alongside `Email/` and `Captcha/`.**
  - Argument FOR: Fits the existing substrate-tier package geometry; allows
    full ADR 0096 mock-first discipline reuse.
  - Argument AGAINST: Cryptographic primitives are NOT integrations in the
    "external system bridge" sense. `Foundation.Integrations` (ADR 0013)
    codifies *provider-neutrality for external vendor surfaces*; Argon2id is
    not a vendor. Sub-option γ conflates "internal cryptographic primitive"
    with "external vendor integration."

**Recommendation.** **Sub-option α — new `packages/foundation-password-hashing/`.**
- Establishes a clean home for future password-policy primitives if they ever
  surface (breach-pwd-list integration; password-strength meter; etc.).
- Avoids the semantic conflation Sub-option β introduces.
- Avoids the vendor-substrate framing Sub-option γ introduces.
- Matches the precedent that ADR 0095 set: a new substrate primitive earns
  its own package (`foundation-bootstrap`) rather than being co-located.

Halt 3 captures this as the load-bearing decision.

---

## 4. Canonical pattern specification (consequences of Option C + Option F + Sub-option α)

The canonical pattern this ADR codifies — if all recommendations RATIFY — is:

### 4.1 Substrate-tier types (new `packages/foundation-password-hashing/`)

**`Sunfish.Foundation.PasswordHashing.Argon2idPasswordHasher<TUser>`** — concrete
implementation of `Microsoft.AspNetCore.Identity.IPasswordHasher<TUser>` using
Konscious.Argon2; reads + writes Argon2id-canonical wire format; verifies
legacy ASP.NET Identity V3 PBKDF2 byte[] format with `SuccessRehashNeeded`
return on success; parameters pinned per Halt 1.

**`Sunfish.Foundation.PasswordHashing.Argon2idHashOptions`** — DI-bound options
type carrying:
- `MemoryKib` (uint; default per Halt 1 — recommended 19456 = 19 MiB OWASP minimum).
- `Iterations` (uint; default per Halt 1 — recommended 2).
- `DegreeOfParallelism` (uint; default per Halt 1 — recommended 1).
- `SaltLengthBytes` (uint; default 16).
- `HashLengthBytes` (uint; default 32).
- Optional `Pepper` (`byte[]?` for static-secret augmentation per Halt 7;
  recommended null = disabled at MVP).

**`Sunfish.Foundation.PasswordHashing.MockPasswordHasher<TUser>`** (Halt 4 if
ratified) — dev/test `IPasswordHasher<TUser>` impl; `HashPassword` returns
`"mock-hash-of-len-" + password.Length`; `VerifyHashedPassword` returns
`Success` iff the candidate's length matches the stored mock-hash's length
(deliberately weak; **dev-only**).

**`Sunfish.Foundation.PasswordHashing.IMockPasswordHasher`** (Halt 4 if ratified)
— empty marker interface; `MockPasswordHasher<TUser>` carries it.

### 4.2 Substrate-tier production-guard (Halt 4 if ratified)

**`Sunfish.Foundation.PasswordHashing.DependencyInjection.MockPasswordHasherProductionGuardAssertion : IHostedService`**
— runs at `StartAsync`; descriptor-scans `IPasswordHasher<>` registrations
(typed via reflection over `ServiceDescriptor.ServiceType`); for any descriptor
whose `ImplementationType` (or `ImplementationInstance`) implements
`IMockPasswordHasher`, requires either:
- `ASPNETCORE_ENVIRONMENT` not equal to `"Production"` (case-insensitive), OR
- `SUNFISH_ALLOW_MOCK_PASSWORD_HASHER` env var parses to `true` via `bool.TryParse`
  (canonical BCL semantics — same fail-closed discipline as ADR 0096
  `SUNFISH_ALLOW_MOCK_PROVIDERS`).

Otherwise throws `MockPasswordHasherInProductionException` at `StartAsync`
naming the offending registration.

### 4.3 Substrate-tier DI helpers

**`Sunfish.Foundation.PasswordHashing.DependencyInjection.PasswordHashingServiceCollectionExtensions`**:
- `AddSunfishPasswordHashing<TUser>(this IServiceCollection, Action<Argon2idHashOptions>? configure = null)`
  — registers `Argon2idPasswordHasher<TUser>` as `IPasswordHasher<TUser>` (singleton);
  binds `Argon2idHashOptions` via `IOptions<Argon2idHashOptions>` (or directly per
  `IOptionsMonitor<T>` pattern; Halt-confirm idiom).
- `AddSunfishMockPasswordHashing<TUser>(this IServiceCollection)` (Halt 4 if
  ratified) — registers `MockPasswordHasher<TUser>` as `IPasswordHasher<TUser>` (singleton);
  registers `MockPasswordHasherProductionGuardAssertion` as
  `IHostedService` (singleton).

### 4.4 Migration mechanism (load-bearing)

On verify, `Argon2idPasswordHasher<TUser>.VerifyHashedPassword(...)` inspects the
stored hash's leading format token:

1. **`$argon2id$` prefix** → Argon2id format (Sunfish canonical going forward).
   Verify with Konscious. If parameters embedded in the hash (m=, t=, p=)
   are at or above the current `Argon2idHashOptions` floor → `Success`.
   If parameters below floor → `SuccessRehashNeeded` (parameter-floor upgrade).

2. **Base64-decode-able to byte[] starting with 0x01** → ASP.NET Identity V3
   PBKDF2 format (legacy from W#79 MVP). Delegate to a privately-composed
   `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>` instance. On success
   → return `SuccessRehashNeeded` (algorithm-upgrade trigger). On failure →
   `Failed`.

3. **Else** → `Failed` (corrupt hash; log + return).

The W#80 login handler observes `SuccessRehashNeeded`, calls `HashPassword(...)`
to generate a fresh Argon2id hash, and writes the new hash back via
`IUserRepository.UpdatePasswordHashAsync(userId, newHash)`. The migration is
**lazy** (only on next login) — see Halt 5 for whether to add background bulk
rehash.

### 4.5 W#79 SignupHandler integration

Per the W#79 sec-eng A + .NET-arch K1 invariant, the SignupHandler signature is
unchanged:

```csharp
public sealed class SignupHandler
{
    private readonly IPasswordHasher<UserEntity> _passwordHasher;
    // ... ctor injects IPasswordHasher<UserEntity> as today (W#79 Rev 2)

    public async Task<SignupResult> HandleAsync(...)
    {
        // STEP 2b — constant-work (sec-eng D) — UNCHANGED
        var stubUser = new UserEntity { Email = request.Email.Trim().ToLowerInvariant() };
        var passwordHashCandidate = _passwordHasher.HashPassword(stubUser, request.Password);
        // ...
    }
}
```

The composition-root registration (`Sunfish.Bridge.Onboarding.Program` or the
new `Sunfish.Bridge.Onboarding.DependencyInjection` static class) changes from:

```csharp
// W#79 ship (PBKDF2 V3 default)
services.AddSingleton<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>();
```

to:

```csharp
// ADR 0097 production cutover
services.AddSunfishPasswordHashing<UserEntity>(options => {
    options.MemoryKib = 19456;        // 19 MiB OWASP minimum (Halt 1)
    options.Iterations = 2;
    options.DegreeOfParallelism = 1;
});
```

Zero handler-tier changes. Zero callsite changes. Composition-root DI swap only.
The sec-eng A + .NET-arch K1 invariant is preserved by construction.

---

## 5. Halt conditions for Admiral

The following halts surface decisions Admiral must rule on before authoring ADR
0097 Rev 1. Each names the recommendation, the alternatives, the trade, and
the source of authority.

### Halt 1 — Argon2id parameters (memory / time / parallelism)

**Question.** What `(MemoryKib, Iterations, DegreeOfParallelism)` triple
becomes the substrate-tier default for `Argon2idHashOptions`?

**Recommended option.** **(19456, 2, 1)** — OWASP minimum (m=19 MiB / t=2 / p=1).

**Alternatives.**
- (a) **(19456, 2, 1)** — OWASP minimum. ~50-100 ms wall-clock on modern x86;
  matches the W#79 sec-eng D constant-work calibration; matches the cheatsheet's
  primary recommendation.
- (b) **(47104, 1, 1)** — OWASP alternative (m=46 MiB / t=1 / p=1). Same
  cryptographic security via different memory/time trade; ~50-100 ms;
  preferred where memory is plentiful and the iteration count must be low
  (rare for password hashing — login latency dominates, not iteration count).
- (c) **(65536, 3, 1)** — Industry-hardened "security-conscious" floor
  (m=64 MiB / t=3 / p=1). ~250-500 ms; meaningfully more expensive but more
  defensive. **Breaks the W#79 sec-eng D ±25% p50/p95 latency parity floor**
  unless re-tuned.
- (d) **(131072, 3, 1)** — Maximum defensive (m=128 MiB / t=3 / p=1). ~500 ms+;
  may degrade UX-perceived signup/login latency.

**Trade.**
- Higher m + higher t = stronger defense vs offline cracking, BUT
- Higher cost = slower legitimate logins + more memory pressure on the API host
  + breaks the sec-eng D constant-work latency parity floor for W#79's
  per-request budget.

**Rationale for recommendation.** OWASP minimum (m=19 MiB / t=2 / p=1) is the
canonical cheatsheet recommendation; matches the W#79 sec-eng D timing
calibration; provides clear cryptographic security margin; can be hardened
later via the parameter-floor upgrade mechanism (§4.4 sub-case 1) without
invalidating existing hashes (rehash-on-next-login auto-upgrades).

**Authority.** Sec-eng dual-council (cryptography-relevance).

### Halt 2 — NuGet library choice

**Question.** Which Argon2id NuGet package does ADR 0097 codify?

**Recommended option.** **`Konscious.Security.Cryptography.Argon2` v1.3.1** —
the OWASP-cheatsheet-recommended .NET Argon2 library.

**Alternatives.**
- (a) `Konscious.Security.Cryptography.Argon2` v1.3.1 — Argon2 1.3 spec
  conformant; MIT licensed; ~700k downloads; OWASP-cheatsheet recommended.
- (b) `Shane32.Argon2` v1.1.0 — alternative .NET impl; smaller adoption;
  MIT licensed; no clear advantage.
- (c) `NetDevPack.Security.PasswordHasher` — wraps Konscious with ASP.NET
  Identity integration; pulls Konscious transitively; adds opinionated layer
  Sunfish doesn't need.
- (d) Custom Sunfish implementation — **REJECT**; don't roll-our-own
  cryptography per universal substrate discipline.

**Trade.**
- Konscious is the de-facto canonical; deeper review surface = more eyeballs.
- Konscious's last major release was 1.3.1 (2024-11-05); maintenance posture
  is "active-but-quiet"; no security advisories on the package.

**Rationale for recommendation.** Konscious matches the OWASP cheatsheet's
direct recommendation; ~700k NuGet downloads is enough adoption surface for
supply-chain confidence; MIT license is fleet-compatible.

**Authority.** Sec-eng dual-council (supply-chain + cryptography).

### Halt 3 — Cluster home for the substrate

**Question.** Where does the `Sunfish.Foundation.PasswordHashing.*` namespace +
package live?

**Recommended option.** **Sub-option α — new `packages/foundation-password-hashing/`.**

**Alternatives.**
- (α) New `packages/foundation-password-hashing/` — substrate-tier own package;
  parallels `foundation-bootstrap` (ADR 0095 precedent).
- (β) Co-located in `packages/foundation-authorization/` — semantically
  related (authn cluster); avoids new-package overhead.
- (γ) Co-located in `packages/foundation-integrations/` — fits the
  substrate-tier geometry alongside `Email/`, `Captcha/`; allows ADR 0096
  Tier-2 mock-first discipline direct reuse. Rejected as a Tier-2-vendor-
  substrate framing per §3.5 / §3.7.

**Trade.**
- α: cleanest substrate-discipline shape; +1 package overhead.
- β: cheaper but conflates authn-credential-storage (hash) with
  authorization-context (tenant/caller/policy).
- γ: rejected for semantic reasons.

**Rationale for recommendation.** ADR 0095 precedent: a new substrate primitive
earns its own package. The cluster-confusion cost of Sub-option β is the
load-bearing concern.

**Authority.** .NET-architect dual-council (substrate-package geometry).

### Halt 4 — Mock-first discipline at Tier-1 boundary

**Question.** Does `Sunfish.Foundation.PasswordHashing` ship a `MockPasswordHasher`
+ `IMockPasswordHasher` marker + `MockPasswordHasherProductionGuardAssertion`
in the Step 1 substrate PR? Or is the mock deferred / omitted?

**Recommended option.** **Ship the mock-first discipline at Tier-1 boundary**
— see Option F + §3.6 rationale.

**Alternatives.**
- (a) Ship `MockPasswordHasher` + production-guard at Step 1.
- (b) Ship `MockPasswordHasher` (no production-guard); rely on developer
  discipline + code review.
- (c) Skip the mock entirely; tests use the real Argon2idPasswordHasher with
  reduced parameters (m=1024, t=1, p=1) for speed.
- (d) Ship mock + production-guard, but in a separate Step 1.5 PR after Step 1
  substrate is GREEN-merged.

**Trade.**
- (a) Full discipline; +80-100 LOC; fast unit-test loops; production-safety
  invariant from day one.
- (b) Mock without guard re-opens "mock silently leaks to production" foot-gun;
  rejected.
- (c) Avoids the mock class entirely; trades unit-test speed for substrate
  simplicity. Tests bypass the algorithm-correctness path; but production-tier
  algorithm correctness is asserted in dedicated cryptographic unit tests
  separately.
- (d) Defer-and-ship-later; introduces a window where production-safety
  property is partially-installed.

**Rationale for recommendation.** Option (a) — the marker + production-guard
pattern is a $80-100-LOC investment that closes the silent-mock-leak foot-gun
permanently. The pattern is already proven at Tier-2 (ADR 0096); cross-tier
application is the cheaper-than-rediscovery move.

**Authority.** Sec-eng dual-council (substrate-discipline + production-safety).
Possibly CIC for the cross-tier discipline-application question.

### Halt 5 — Migration strategy from PBKDF2 to Argon2id

**Question.** When does the bulk of PBKDF2 hashes get upgraded to Argon2id?

**Recommended option.** **Lazy: rehash-on-next-login only** (no background
bulk-rehash job).

**Alternatives.**
- (a) Lazy: rehash-on-next-login only. Idle accounts retain PBKDF2 hashes
  indefinitely (until first login post-ADR-0097-cutover).
- (b) Eager: background bulk-rehash job iterates all `User` rows; rehashes
  each. Requires storing the plaintext password — **IMPOSSIBLE**; passwords
  are not retrievable post-hash.
- (c) Hybrid: lazy-on-login + force-password-reset email to all users with
  PBKDF2 hashes within N days; expires hash that's stale beyond a TTL.

**Trade.**
- (a) Cheapest; relies on user login activity; idle accounts have weaker
  hash indefinitely (but PBKDF2 100k is OWASP-acceptable; the upgrade
  improves margin, not closes a vulnerability).
- (b) Impossible (no plaintext).
- (c) Stronger; UX cost (forced password reset annoys users); compliance
  posture stronger.

**Rationale for recommendation.** Option (a) — PBKDF2 100k is not vulnerable;
the migration is a hardening upgrade, not a remediation. Lazy migration is
the cheapest discipline; the migration-window cohort is small (W#79 MVP
launch → ADR 0097 production cutover; expected weeks-to-months, not years).
Option (c) is a downstream W#80+ concern if MVP delays cutover.

**Authority.** Admiral + sec-eng (compliance + UX trade).

### Halt 6 — Hash output format

**Question.** What is the Sunfish-canonical Argon2id hash wire format?

**Recommended option.** **Konscious-canonical Argon2id PHC string format** —
`$argon2id$v=19$m=19456,t=2,p=1$<base64-salt>$<base64-hash>`.

**Alternatives.**
- (a) PHC string format `$argon2id$v=19$m=19456,t=2,p=1$<salt>$<hash>` (OWASP-canonical wire format; self-describing).
- (b) ASP.NET Identity-style byte[] with a custom Sunfish version byte
  (e.g., 0x02 for Argon2id; byte[] then Base64-encoded for storage).
- (c) Custom JSON shape `{"algo":"argon2id","v":19,"m":19456,...,"salt":...,"hash":...}`.

**Trade.**
- (a) Self-describing; OWASP-canonical; instantly recognizable by ops + sec
  tooling; parsable by any Argon2id-aware tool; allows parameter-floor
  upgrade detection.
- (b) Smaller storage (~80 bytes vs ~100 bytes for PHC); but composition
  with ASP.NET Identity's V3 PBKDF2 byte[] format (0x01-prefixed) requires
  the verify path to discriminate by leading byte AND by Base64-decode-vs-
  PHC-string detection. More fragile.
- (c) JSON in `User.PasswordHash` is non-standard; larger; no industry tool
  reads JSON-wrapped hashes.

**Rationale for recommendation.** PHC string format is the industry standard
(RFC 9106 references it; OWASP cheatsheet uses it; every Argon2id implementation
across languages reads/writes it). Self-describing means future parameter
hardening detects "this hash was made with m=19456 but the current floor is
m=46080" without auxiliary metadata storage.

**Authority.** Sec-eng + .NET-architect (substrate-tier wire format).

### Halt 7 — Pepper / static-secret augmentation

**Question.** Does the Argon2id implementation XOR a server-side static
"pepper" into the password material before hashing?

**Recommended option.** **NO pepper at MVP** (`Argon2idHashOptions.Pepper`
default null = disabled).

**Alternatives.**
- (a) No pepper. Salt is per-user random; Argon2id parameters provide the
  memory-hard defense; pepper adds defense-in-depth but introduces
  secret-management substrate dependency (pepper rotation, pepper compromise
  semantics — pepper compromise + hash compromise = same risk as no pepper).
- (b) Pepper from `IConfiguration["Sunfish:Argon2id:Pepper"]` — env-bound;
  application-tier secret.
- (c) Pepper from a future secret-store substrate (e.g., Azure Key Vault /
  HashiCorp Vault) — proper rotation + audit; out-of-scope for ADR 0097.

**Trade.**
- (a) Simpler; substrate has no secret dependency.
- (b) Adds secret config; rotation semantics undefined.
- (c) Proper substrate; requires the secret-store substrate ADR (does not
  exist yet) as a dependency.

**Rationale for recommendation.** Option (a) — OWASP cheatsheet treats pepper
as **optional** defense-in-depth, not a baseline requirement. Argon2id's
memory-hard parameter provides the primary defense; the pepper substrate
should be added only after the secret-store substrate ADR exists.
`Argon2idHashOptions.Pepper` SHOULD exist as a nullable `byte[]?` field so
future enabling is a config change, not an algorithm change.

**Authority.** Sec-eng + Admiral (defense-in-depth vs substrate-scope trade).

### Halt 8 — Pre-production gate sequencing

**Question.** When does ADR 0097 land relative to W#79 sub-cohort 1 production?

**Recommended option.** **ADR 0097 Steps 1–2 (substrate + W#80 login migration
read-side) MERGE before W#79 sub-cohort 1 reaches MVP production-customer
onboarding.**

**Alternatives.**
- (a) ADR 0097 lands before MVP launch (broadest; entire substrate-tier
  prepared in advance).
- (b) ADR 0097 lands between MVP launch and production-customer onboarding
  (narrower; depends on whether these are separate milestones).
- (c) ADR 0097 lands after production-customer onboarding (REJECT;
  contradicts H8 commitment "before W#79 reaches production").

**Trade.**
- (a) Cleanest; ensures zero PBKDF2 hashes ever exist in production data.
- (b) Acceptable per H8; some PBKDF2 hashes exist; lazy migration upgrades
  them on next login.
- (c) Rejected per H8.

**Rationale for recommendation.** Per H8 ruling: "ADR 0097 MUST land before
W#79 reaches production." Sequencing options (a) and (b) are both H8-conformant;
(a) is preferred for clean cutover and zero-migration-cohort-size.

**Authority.** Admiral + CIC (MVP launch sequencing).

### Halt 9 — Council review cadence

**Question.** Confirm sec-eng dual-council MANDATORY on ADR 0097 text + each
Step PR, per H8?

**Recommended option.** **CONFIRM** — sec-eng + .NET-architect dual-council
MANDATORY on ADR 0097 text; sec-eng MANDATORY SPOT-CHECK on Step 1 (substrate)
PR; sec-eng + .NET-architect SPOT-CHECK on Step 2 (W#80 login-handler
adoption); SPOT-CHECK on Step 3 (optional background bulk-rehash) if pursued.

**Alternatives.** None substantive — H8 ruling pins this.

**Authority.** Admiral (confirm) + already-ruled (H8).

### Halt 10 — Migration backward-compat window posture

**Question.** During the W#79-MVP-ship → ADR-0097-production-cutover window,
do existing PBKDF2 hashes continue to verify after cutover?

**Recommended option.** **YES — Argon2idPasswordHasher.VerifyHashedPassword
delegates to a private `PasswordHasher<TUser>` for legacy V3 format; returns
`SuccessRehashNeeded` on legacy-hash verify success** (per §4.4).

**Alternatives.**
- (a) Yes — back-compat verify + auto-rehash on next login (recommended).
- (b) No — force password reset on all W#79-MVP-era users at cutover.
  Brittle UX; rejected.
- (c) Yes for N days post-cutover; then force reset. Adds a TTL window;
  unnecessary complexity.

**Rationale for recommendation.** Option (a) is the industry-standard upgrade
pattern. ASP.NET Identity's `SuccessRehashNeeded` enum value exists exactly
for this case. The W#80 login handler's call site already integrates the
trigger per §4.4.

**Authority.** Admiral + UX + sec-eng (UX vs hard-cutover trade).

### Halt 11 — IOptionsMonitor<Argon2idHashOptions> vs IOptions<Argon2idHashOptions>

**Question.** Does `Argon2idPasswordHasher<TUser>` consume `IOptions<Argon2idHashOptions>`
(captured-at-construction; immutable) or `IOptionsMonitor<Argon2idHashOptions>`
(runtime-reloadable)?

**Recommended option.** **`IOptions<Argon2idHashOptions>`** — captured at
singleton construction; immutable for the process lifetime; substrate-tier
ADR-ratified parameter changes ship via deployment, not runtime config reload.

**Alternatives.**
- (a) `IOptions<T>` — singleton-snapshot at process start.
- (b) `IOptionsMonitor<T>` — runtime-reloadable on `appsettings.json` change.
- (c) `IOptionsSnapshot<T>` — per-request scope; **REJECT** (singleton
  scope can't depend on per-request scope; service-locator anti-pattern).

**Trade.**
- (a) Simpler; matches "substrate-tier change = deployment-tier change"
  discipline.
- (b) Reloadable but unnecessary; introduces a window where mid-running
  hashes use different parameters than newly-issued ones — confusing
  on hash-verify-discrimination paths.

**Rationale for recommendation.** Option (a) — substrate-tier parameters
should not silently change in a running process. Parameter changes require
ADR-tier ratification, which is by definition a deployment-tier event.

**Authority.** .NET-architect dual-council (idiomatic .NET DI).

---

## 6. Implementation roadmap — Step PRs

Each Step is a separate PR per cohort-cohort-pattern; each is scoped to a
single review surface:

### Step 1 — `foundation-password-hashing` substrate package (load-bearing)

- New project `packages/foundation-password-hashing/Sunfish.Foundation.PasswordHashing.csproj`.
- New types per §4.1 + §4.2 + §4.3.
- NuGet PackageReference: `Konscious.Security.Cryptography.Argon2` v1.3.1
  (or per Halt 2).
- ProjectReference: `Microsoft.AspNetCore.Identity.Core` (for `IPasswordHasher<TUser>`
  interface).
- Tests: `packages/foundation-password-hashing/tests/Sunfish.Foundation.PasswordHashing.Tests.csproj`
  with comprehensive coverage:
  - Argon2id hash + verify round-trip (deterministic via fixed salt).
  - Legacy V3 PBKDF2 hash verify + `SuccessRehashNeeded` return.
  - Mock hash + verify round-trip.
  - Production-guard assertion throws on mock + Production env.
  - Production-guard assertion no-op on mock + opt-out env var.
  - PHC string format parse robustness (malformed input → `Failed`).
  - Parameter-floor upgrade triggers `SuccessRehashNeeded` on
    below-floor-parameter hashes.

**Dual-council MANDATORY**: sec-eng (cryptography + production-safety) +
.NET-architect (substrate-package geometry).

### Step 2 — W#80 login handler integration (read-side migration trigger)

- Pre-requisite: W#80 sub-cohort 1 Stage-05 hand-off authored + W#80
  login-handler PR (a downstream sub-cohort; out of ADR 0097 scope at the
  authoring tier; ADR 0097 references the W#80 PR as the integration site).
- W#80 login handler observes `PasswordVerificationResult.SuccessRehashNeeded`
  + calls `_passwordHasher.HashPassword(...)` + persists fresh hash via
  `IUserRepository.UpdatePasswordHashAsync(userId, newHash)`.

**SPOT-CHECK MANDATORY**: sec-eng (migration-trigger correctness) +
.NET-architect (idiomatic upgrade pattern).

### Step 3 (optional) — Background bulk-rehash job

- Per Halt 5 disposition: **NOT recommended** at MVP; defer indefinitely
  unless lazy-migration coverage proves insufficient post-cutover.
- If pursued: `Sunfish.Bridge.Onboarding.BackgroundJobs.PasswordHashRehashJob`
  scheduled job that iterates `User` rows with PBKDF2-format
  `PasswordHash` — but **cannot rehash without plaintext**, so this Step
  is fundamentally a "force password reset by email" UX flow, not a hash
  upgrade job. Rejected.

### Step 4 — W#79 Onboarding cluster cutover to `AddSunfishPasswordHashing<UserEntity>`

- Replace `services.AddSingleton<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>()`
  (W#79 ship) with `services.AddSunfishPasswordHashing<UserEntity>(options => ...)`
  (ADR 0097 production cutover).
- Composition-root-only change in `Sunfish.Bridge.Onboarding.DependencyInjection`.
- Zero handler-tier changes (sec-eng A + .NET-arch K1 invariant preserved by
  construction).

**SPOT-CHECK MANDATORY**: sec-eng (composition-root correctness +
production-safety invariant verification).

---

## 7. Alternatives considered (rejected at scaffold tier)

Beyond §3.1–§3.6:

- **Roll-our-own Argon2id implementation in C#** — REJECT; "don't roll your
  own cryptography" applies; Konscious is canonical.
- **Use `System.Security.Cryptography.PasswordDerivedBytes`** — REJECT; only
  supports PBKDF2.
- **Use `Microsoft.AspNetCore.Cryptography.KeyDerivation`** — REJECT; only
  supports PBKDF2 (the same primitive ASP.NET Identity's V3 uses internally).
- **Migrate to Identity Server / Auth0 / Okta** — REJECT; out of scope for
  Sunfish (separate vendor-substrate decision; ADR-tier on its own).

---

## 8. Open questions (forward-watch)

Items NOT addressed by ADR 0097 but worth surfacing for downstream ratification:

1. **Breach password list integration (HaveIBeenPwned)** — Should
   `Argon2idPasswordHasher.HashPassword` reject passwords appearing in the
   HIBP top-100k breach list? Application-tier concern; W#80 password-policy
   ADR territory if pursued.
2. **Audit-event emission on hash upgrade** — Should the W#80 login handler
   emit `user_password_hash_upgraded` audit event when `SuccessRehashNeeded`
   is observed + rehash succeeds? ADR 0049 enum extension; W#80 territory.
3. **Pepper rotation procedure** — If Halt 7 future-enables pepper, what
   is the rotation flow? Old-pepper-hashes verify fail; force-reset is
   the only path. Pepper substrate ADR territory; FORWARD-WATCH.
4. **Compliance posture for FIPS-mandated environments** — If Sunfish ever
   adopts FIPS validation (no current commitment), Argon2id is not
   FIPS-approved. The substrate would need a FIPS-mode toggle that
   falls back to PBKDF2-HMAC-SHA256. FORWARD-WATCH; out of ADR 0097
   scope unless FIPS becomes a near-term concern.
5. **Per-tenant Argon2id parameter override** — Some compliance-heavy
   tenants may demand m=128 MiB. Per-tenant override would require
   `Argon2idHashOptions` to be tenant-keyed. NOT recommended at MVP;
   FORWARD-WATCH if enterprise tenants surface the requirement.

---

## 9. Sources cited

### Primary (industry consensus + standards + reference implementations)

1. OWASP Password Storage Cheat Sheet — `https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html` — retrieved 2026-05-27.
2. IETF RFC 9106 "Argon2 Memory-Hard Function for Password Hashing and Proof-of-Work Applications" — 2021-09 — `https://datatracker.ietf.org/doc/rfc9106/`.
3. NIST SP 800-63B-3 "Digital Identity Guidelines: Authentication and Lifecycle Management" — 2017-06 (errata current as of 2026-05-27) — `https://pages.nist.gov/800-63-3/sp800-63b.html`.
4. OWASP Application Security Verification Standard 4.0.3 §2.4 "Credential Storage Requirements" — 2024-10 — `https://owasp.org/www-project-application-security-verification-standard/`.
5. ASP.NET Core Identity `PasswordHasher<TUser>` source — `https://github.com/dotnet/aspnetcore/blob/main/src/Identity/Extensions.Core/src/PasswordHasher.cs` — retrieved 2026-05-27.
6. Konscious.Security.Cryptography.Argon2 NuGet (v1.3.1) — `https://www.nuget.org/packages/Konscious.Security.Cryptography.Argon2` — retrieved 2026-05-27.
7. Konscious.Security.Cryptography GitHub source — `https://github.com/kmaragon/Konscious.Security.Cryptography` — retrieved 2026-05-27.

### Secondary (industry implementation precedent + framework decisions)

8. ADR 0013 Foundation.Integrations — `shipyard/docs/adrs/0013-foundation-integrations.md` — provider-neutrality discipline precedent.
9. ADR 0091 R2 ITenantContext Divergence Resolution — `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` — `TenantContextRegistrationAssertion` IHostedService "POSITIVE invariant" precedent.
10. ADR 0095 Bootstrap Context — `shipyard/docs/adrs/0095-bootstrap-context.md` — substrate-tier ADR cadence + `BootstrapAndTenantMutualExclusionAssertion` startup-ordering precedent.
11. ADR 0096 Tier-2 Vendor-Provider Substrate — `shipyard/docs/adrs/0096-tier-2-vendor-provider-substrate.md` — mock-first discipline + `IMockVendorProvider` marker + `MockProviderProductionGuardAssertion` precedent.
12. ADR 0098 Block-Naming Generalization — `shipyard/docs/adrs/0098-block-naming-generalization.md` — substrate-tier ADR Rev 2 cadence precedent.
13. W#79 sub-cohort 1 Stage-05 hand-off Rev 2 — `shipyard/.worktrees/onr-w79-stage-05-handoff/icm/05_implementation-plan/cohort-w79-hand-off.md` — sec-eng A + .NET-arch K1/K2 + sec-eng D invariants.
14. Admiral W#79 ruling H8 — `coordination/inbox/admiral-ruling-2026-05-26T0035Z-w79-stage-05-9-halts-resolved.md` — ADR 0097 follow-on commitment.

### Tertiary (industry commentary; flagged anecdotal)

15. "Password Hashing Algorithms Compared" (BellatorCyber blog, 2023) — `https://bellatorcyber.com/blog/best-password-hashing-algorithms-of-2023` — Argon2id vs alternatives commentary.
16. "How to Use Argon2 for Password Hashing in C#" (Twelve21 blog, by Konscious primary author Kevin Maragon, 2022) — `https://www.twelve21.io/how-to-use-argon2-for-password-hashing-in-csharp/` — Konscious usage worked example.
17. "How to Choose the Right Parameters for Argon2" (Twelve21 blog, 2022) — `https://www.twelve21.io/how-to-choose-the-right-parameters-for-argon2/` — Argon2 parameter selection.
18. "Password Hashing Guide 2025" (guptadeepak.com, 2025) — `https://guptadeepak.com/the-complete-guide-to-password-hashing-argon2-vs-bcrypt-vs-scrypt-vs-pbkdf2-2026/` — comparative analysis.
19. diegofercri/password-hasher GitHub — `https://github.com/diegofercri/password-hasher` — alternative C# Argon2id reference implementation + benchmarks.

---

— ONR, 2026-05-27T14:25Z (estimated finish time at authoring)
