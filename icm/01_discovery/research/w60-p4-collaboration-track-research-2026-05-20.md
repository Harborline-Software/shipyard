# ONR research — W#60 Phase 4 Collaboration Track Architecture (2026-05-20)

**Requester:** Admiral (per `admiral-directive-2026-05-19T22-50Z-onr-research-queue-batch-dispatch.md` item #2, amended by `admiral-directive-amendment-2026-05-20T03-50Z-onr-research-queue-item-2-w60-p4-canonical-shape.md`)
**Authored by:** ONR
**Authored at:** 2026-05-20T11-55Z
**Status:** draft (ratification pending CIC review + sec-eng / .NET-architect council review on the multi-role authorization model)

---

## Scope of investigation

- **In scope:** the canonical Phase 4 architecture per `shipyard/icm/_state/handoffs/w60-collaboration-phase4-stage06-handoff.md` — Bridge-role-account access (NOT peer-sync), Tauri Stronghold auth hardening, multi-role authorization (Accountant + CPA + Tenant), magic-link patterns, bank CSV ingest, tenant portal app shape.
- **Out of scope:** Phase 5 peer-sync architecture (Headscale-mesh + SQLite + Loro CRDT financial entities) — referenced as a future-ADR note only; deliberately deferred per W#60 P4 hand-off §"Architecture decision (Phase 4)".
- **Authoritative sources consulted:** W#60 P4 Stage-06 hand-off (primary); current `credentialStore.ts` + `AuthenticatedTenantPolicy.cs` + `DemoTenantContext.cs` source; W#60 P4 Cargo.toml; ADR 0091 Rev 2 (Accepted; ITenantContext shape); ADR 0061 (Headscale mesh; Phase 5 reference only); ADR 0086 (Tauri React product surface).
- **Success looks like:** Engineer + po-mac + po-win read this doc and know (a) what's shipped vs in flight vs not started for each Phase 4 PR; (b) the canonical Bridge-role-account model and how to scaffold it; (c) the magic-link contract; (d) the bank CSV format-mapping shape; (e) Phase 5 framing so they don't accidentally implement peer-sync inside Phase 4.

---

## TL;DR

1. **PR 1 (Stronghold + auth hardening) is substantially shipped.** `credentialStore.ts` + `credentialStore.test.ts` + `keyring v3` platform-native backends are on main; Council A1.1 design reviewed (machine-locked master key, OS-keychain derivation per platform). Remaining work in PR 1: first-launch UI redirect to Bridge `/auth/login?redirect=tauri://localhost` (verify Bridge allowed-redirects list); logout flow.

2. **PR 2 (Accountant Bridge role) has a substrate gap.** Current `AuthenticatedTenantPolicy` requires only `RequireAuthenticatedUser()` — no role-scoped policy layer. `DemoTenantContext` hardcodes `Roles = [Manager]` + `HasPermission => true`. PR 2 needs both a role-scoped policy layer (`AccountantPolicy` / `CpaPolicy` / `TenantPolicy` design choice — single claims-based fan-out vs N policies — open question for .NET-architect council) and a claims-backed replacement for `DemoTenantContext` that reads actual roles from the session token.

3. **PR 3 (CPA + Tenant portal) shape is well-specified in the hand-off but introduces a new app.** `apps/tenant-portal/` is a standalone Vite + React + `@sunfish/ui-react` build separate from `apps/anchor-tauri/`. Magic-link pattern reuses W#18 precedent (`VendorMagicLinkIssued`). Open question: in-memory v1 token store vs persistent (SQLite/Redis) for production — hand-off says in-memory OK for v1.

4. **PR 4 (Bank CSV import) needs format research.** Bank CSV formats vary widely (Chase, BofA, Wells Fargo, generic QuickBooks-friendly); column-mapping UI must be flexible. Hand-off specifies `localStorage` persistence v1; per-bank presets are a follow-on.

5. **PR 5 (close-out + deployment + Windows arch-detection page).** Multi-arch download page per ADR 0088 Approach C-now (UA-string detection); MSIX/WiX Approach B deferred; bootstrapper Approach A skipped.

6. **Phase 5 (peer-sync) is a separate-ADR future workstream.** ADR 0061 Headscale mesh + ADR 0086 Tauri React are the architectural ancestors; Phase 5 needs a dedicated ADR designing the conflict-resolution + Loro CRDT extension to financial entities + Headscale device registration. **Phase 4 must NOT implement peer-sync** — that's the canonical mistake the amendment 2026-05-20T03:50Z corrects.

---

## 1. Current-state map (per Phase 4 PR)

### PR 1 — Tauri Stronghold + auth hardening (substantially shipped)

**Source surface verified:**

- `sunfish/apps/desktop/src-tauri/Cargo.toml` (lines 13-30 verified 2026-05-20):
  - `tauri-plugin-stronghold = "2"` ✓
  - `keyring = { version = "3", features = ["windows-native", "apple-native", "sync-secret-service"] }` ✓ (Council A1.1: platform-native backends explicit; without features keyring v3 silently falls back to in-process mock)
  - `getrandom = "0.3"` ✓
- `sunfish/apps/desktop/src/services/credentialStore.ts` (98 lines verified 2026-05-20):
  - `init()` / `setToken(token)` / `getToken()` / `clearToken()` / `resetForTesting()` exports
  - Snapshot stored at `${appDataDir()}/anchor.stronghold`
  - Client name `anchor-auth`; key `bridge-token`
  - `STRONGHOLD_INIT_PASSWORD = 'anchor-machine-locked'` — IGNORED by Rust closure (machine-locked OS-keychain derivation); the value is a stable non-empty constant
  - Module-level cache; one snapshot per app lifetime
  - Idempotent `clearToken` (no error if already absent)
- `sunfish/apps/desktop/src/services/credentialStore.test.ts` exists (test count not verified inline)

**Architectural pattern (verified via source review):**

The credential store is a thin TypeScript wrapper around the Tauri Stronghold v2 plugin. The Rust-side closure (in `src-tauri/src/lib.rs`) ignores the JS-supplied password and derives the 32-byte master key from the OS keychain (DPAPI on Windows, Keychain on macOS, Secret Service on Linux). This is per Council A1.1 (security-engineering) — eliminates the JS-side-password attack surface.

**Remaining PR 1 work (per hand-off):**

- First-launch UI: detect missing token in Stronghold → redirect to Bridge `/auth/login?redirect=tauri://localhost`
- Logout flow: `clearToken()` + navigation back to login
- Allowed-redirects verification on Bridge (`tauri://localhost` must be in the allowlist)

**Risks Engineer / po-mac should know:**

1. **OS keychain unavailability.** On a fresh Linux install without `libsecret` (Secret Service), `sync-secret-service` backend fails. App may need a graceful fallback flow ("install gnome-keyring or kwallet"). Verify via `keyring::Entry::set_password()` returning the right error type.
2. **Stronghold snapshot migration.** If `anchor.stronghold` snapshot exists from a prior session BUT the OS keychain entry is missing/corrupted, the snapshot becomes unreadable (machine-locked). Need a recovery path: detect this state, prompt user, clear the snapshot, restart login flow. Currently NOT in PR 1 scope per hand-off — but the failure case is real (e.g., user clears keychain).
3. **Multi-process access.** Tauri single-instance is enforced (`tauri-plugin-single-instance = "2"` in Cargo.toml). Good. But if a stale Tauri process holds the snapshot lock and a fresh instance starts, the single-instance plugin should kill the second; if it doesn't, snapshot save may fail. Test the recovery path.
4. **Token rotation.** If Bridge issues short-lived access tokens with a refresh token, both go in Stronghold. The `bridge-token` key currently holds ONE value — Phase 4 PR 2 (or a follow-on) may need to extend to a refresh-token key. Document the key namespace.

### PR 2 — Accountant Bridge role account + accounting UI (NOT started; substrate gaps)

**Current state of Bridge auth:**

`signal-bridge/Sunfish.Bridge/Authorization/`:
- `AuthenticatedTenantPolicy.cs` — single policy, requires only `RequireAuthenticatedUser()`. Tenant scoping via `ITenantContext.TenantId` resolution inside handlers. **No role check at policy layer.**
- `DemoTenantContext.cs` — implements `ITenantContext` per ADR 0091 R2 (sum-interface). Hardcoded: `TenantId="demo-tenant"`, `UserId="demo-user"`, `Roles=[Manager]`, `HasPermission => true`. Logs a warning on construction (DEMO seam active).
- `DemoAuthWarningFilter.cs` — emits the demo-warning on first request (companion to `DemoTenantContext`).
- `Sunfish.Bridge.Cockpit.CockpitEndpoints.CockpitPolicyName` (existing per W#74 cohort-1) — narrower; requires `role ∈ {owner, spouse}`.

**Gap: no role-scoped policies for Accountant / CPA / Tenant.**

PR 2 needs a coherent design. Two design paths:

**Option A — One policy per role (parallel to `CockpitPolicy`):**

```csharp
public static class AccountantPolicy
{
    public const string PolicyName = "AccountantPolicy";
    public static AuthorizationOptions AddAccountantPolicy(this AuthorizationOptions options)
    {
        options.AddPolicy(PolicyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("role", "accountant");
        });
        return options;
    }
}
// ... CpaPolicy, TenantPolicy similarly
```

- **Pro:** matches existing pattern (CockpitPolicy precedent); each policy is small + auditable; reviewer sees policy intent at a glance.
- **Con:** N policies = N maintenance surfaces; cross-role permission patterns (e.g., "accountant + cpa can both export") force `RequireAssertion` callbacks that duplicate logic.
- **Verdict:** lightweight; matches fleet convention; preferred unless the cross-role pattern emerges.

**Option B — Single claims-based fan-out:**

```csharp
public static class SunfishRoles
{
    public const string Accountant = "accountant";
    public const string Cpa = "cpa";
    public const string Tenant = "tenant";
    public const string Manager = "manager";   // existing
    public const string Owner = "owner";        // existing
}
// Endpoints use RequireAuthorization(policy => policy.RequireClaim("role", SunfishRoles.Accountant))
// inline, no named policy class
```

- **Pro:** declarative; no policy class per role; easier to combine roles inline.
- **Con:** loses the policy-as-documented-intent benefit; harder to audit ("where does AccountantPolicy live?" no longer answerable by file search).
- **Verdict:** less .NET-idiomatic for this fleet; preferred for very small role sets but not for our 5+ roles.

**ONR recommendation:** Option A (one policy class per role). Open question for .NET-architect council confirmation before PR 2 opens. **See open questions §9.**

**`DemoTenantContext` replacement strategy:**

Hand-off PR 2 implies the accountant flow works against the demo context (hardcoded user + role = Manager). But a working accountant flow MEANS the demo user must be able to ASSUME the accountant role for testing. Two approaches:

**Path A — Seed multiple demo users with different roles.** `BridgeSeeder` already seeds `demo-tenant` + `demo-user` (Manager). Extend to seed `demo-accountant` (Accountant role), `demo-cpa` (CPA), `demo-tenant-portal-user` (Tenant). Test fixtures route to the right seed by injecting the right `ITenantContext` mock.

**Path B — Claims-bound `DemoTenantContext`.** Replace hardcoded `Roles = [Manager]` with a configurable list driven by `appsettings.Development.json` (e.g., `"DemoRoles": ["manager", "accountant"]`). Single demo user can wear multiple hats.

**ONR recommendation:** Path A for clarity (separate test identities make role-isolation regressions visible). Path B is acceptable for local-dev convenience but masks role-boundary bugs.

**Open question for sec-eng:** does the demo seam need to be GONE before Phase 4 PR 2 ships, OR can it coexist with a claims-backed seam (development-only feature flag)? ADR 0091 §"Production OIDC-impl ADR (future)" pre-stages this; PR 2 needs an interim answer.

### PR 3 — CPA read-only + Tenant portal (NOT started)

**CPA side (Bridge):**

- New policy: `CpaPolicy` (Option A above; requires `cpa_role` claim) — same pattern as `AccountantPolicy`.
- New endpoints: `GET /api/v1/tax-reporting/year-end-summary?year={year}` + `GET /api/v1/tax-reporting/export` (CSV or JSON).
- Year-end shape: P&L by property + Schedule E categories (US-specific; international tax forms out of scope).
- Audit emission: `TaxReportingViewed` + `TaxReportingExported` (new `AuditEventType` constants).
- Schedule E mapping: income / expenses by category / depreciation / interest. Need ERPNext Chart of Accounts mapping research (out of P4 scope; Engineer + accountant SME work).

**Tenant portal (new app):**

- Location: `apps/tenant-portal/` — standalone Vite + React + `@sunfish/ui-react`; separate build from `apps/anchor-tauri/`.
- Auth flow: `POST /api/v1/auth/magic-link` creates a short-lived JWT (24h) scoped to a single `TenantId`; sends link via crew-comms (SMS/email) using `blocks-crew-comms`.
- Pages: `LeaseView.tsx` (lease details + payment history); `Messages.tsx` (crew-comms thread with CO); `PaymentHistory.tsx` (payment records).
- Server-side data fetching with JWT validation on every request (PII; do NOT client-render).

**Magic-link pattern research (W#18 precedent + cohort-3 considerations):**

W#18 Phase 5 (`kernel-sync`) shipped `AuditEventType.VendorMagicLinkIssued` + `VendorMagicLinkConsumed`. Tenant portal follows the same pattern:

```csharp
public enum MagicLinkAudience
{
    Vendor,    // W#18 precedent
    Tenant,    // W#60 P4 new
    Cpa,       // potential future use; PR 3 hand-off implies CPA also uses links
}

public record MagicLinkToken(
    Guid TokenId,
    MagicLinkAudience Audience,
    string SubjectId,         // tenantId for Tenant audience; cpaUserId for Cpa
    string TenantContextId,   // CO's tenant — the data the magic-link grants access to
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt);
```

**Token store v1:** in-memory `IDictionary<Guid, MagicLinkToken>` keyed on the token GUID; eviction on `ExpiresAt`. Acceptable for v1 per hand-off ("in-memory for v1; redis/db for prod").

**Production-grade token store (forward-watched, not P4 scope):**
- SQLite if Bridge runs single-instance; Redis if Bridge becomes horizontal-scaled.
- Index on `(Audience, SubjectId)` for revocation lookups.
- Audit emission on issuance + consumption + revocation.

**Single-use enforcement:** Bridge handler sets `ConsumedAt` on first valid request; subsequent requests with the same token return 401. Per hand-off PR 3 halt condition: "Magic-link JWT must be single-use OR time-limited (24h)" — v1 can ship either; ONR recommends BOTH (time-limited + single-use) for defense-in-depth.

**Magic-link delivery fallback:** if `blocks-crew-comms` delivery provider is not configured in Integration Atlas, fall back to displaying the link URL in CO's dashboard (CO copies manually). Per hand-off PR 3 halt condition.

**Tenant portal deployment:** standalone Vite app. Three deployment shapes:

| Shape | Description | Pro | Con |
|---|---|---|---|
| **A** | Served from Bridge as static files | Single deploy unit; same domain as Bridge | Bridge process serves UI; coupling |
| **B** | Separate Vercel/Netlify deploy | Independent scaling; CDN-optimized | Two domains; CORS configuration |
| **C** | Served from `apps/anchor-tauri/` via Bridge proxy | One install for CO | Tenants need CO to be online (offline-first concern) |

**ONR recommendation:** **A** (Bridge static). Simplest deployment; same auth domain; offline-first not a concern for the tenant portal (tenants always use the web link). Open question for CIC + .NET-architect council.

### PR 4 — Bank CSV import (NOT started)

**Bank CSV format research:**

Common US bank CSV exports (top 4 banks + QuickBooks-friendly):

| Bank | Date | Amount sign | Description | Reference | Notes |
|---|---|---|---|---|---|
| **Chase** | MM/DD/YYYY | Negative=debit / Positive=credit | "Description" | "Type" + "Trans Date" + "Balance" | Headers vary by account type |
| **Bank of America** | MM/DD/YYYY | Single "Amount" col (signed) | "Description" | "Running Bal." | Has a "Status" col (cleared/pending) |
| **Wells Fargo** | MM/DD/YYYY | Positive only; separate "Amount Debit" + "Amount Credit" cols | "Description" | Sometimes empty "Reference Number" | Quoted strings throughout |
| **Capital One** | MM/DD/YYYY | "Debit" + "Credit" separate cols | "Description" | "Card #" (last 4 digits) | |
| **QuickBooks-friendly (generic)** | YYYY-MM-DD | Signed "Amount" | "Description" | "Payee" + "Account" | The fleet's ideal target shape |

**Implications for PR 4:**

1. **Column mapping is mandatory.** Hand-off specifies "CO maps columns once in UI; mapping persisted in `appsettings.json`" — ONR recommends `localStorage` per hand-off (mapping is per-bank-account; tied to the browser, not the server).
2. **Amount-sign convention is variable.** Need a per-mapping setting: "single signed column" vs "separate debit + credit columns" vs "single column with type indicator (debit/credit)".
3. **Duplicate detection on date + amount + reference.** Hand-off specifies "warn, don't block" — ONR endorses (false-positive duplicate detection blocks the user; warning + user-override is the right balance).
4. **Date format normalization.** Some banks use MM/DD/YYYY; some use YYYY-MM-DD; some use DD/MM/YYYY (UK/international). FED needs a date-parser that asks the user once per mapping ("which date format does your bank use?").
5. **Encoding.** Some bank exports use Windows-1252 / Latin-1 (especially older institutions); browser default UTF-8. Need encoding detection or user-selectable.
6. **CSV variants.** Some banks export tab-separated (TSV) and call it "CSV"; some use semicolons (European convention); some embed newlines in description fields (proper RFC 4180 quoting). Use a robust CSV parser (e.g., `papaparse` for JS or `CsvHelper` for .NET).

**Bank CSV ingest forward-watch (out of P4 scope):**
- Per-bank presets library (one-click "I'm a Chase customer"); requires curation
- OFX / QFX file format support (alternative to CSV; more structured)
- Bank-feed-direct integration (e.g., Plaid) — out of scope for P4; potential P5/P6

### PR 5 — Ledger flip + deployment guide + Windows arch-detection (NOT started)

**Multi-arch Windows installer per ADR 0088:**
- **Approach C (now):** UA-string detection (`navigator.userAgentData.getHighEntropyValues(['architecture'])` with UA-string fallback) — surfaces correct download link (x64 vs ARM64) on the download page. Manual arch toggle included.
- **Approach B (deferred):** Multi-arch MSIX/WiX. Deferred until Windows-ARM test device on tailnet.
- **Approach A (skipped):** Bootstrapper `.exe`. Permanently skipped.

**Deployment guide:** `docker-compose.prod.yml` with ERPNext + MariaDB + Redis + Sunfish Bridge + Nextcloud; persistent volumes; environment variable placeholders. README "Self-hosting" section; target: 20 minutes from `docker-compose up -d` to logged-in dashboard.

**Risk:** ERPNext Frappe v15 has known issues with the older MariaDB versions; Docker compose needs to pin `mariadb:10.6` minimum. Verify Engineer's compose file against current ERPNext compatibility matrix.

---

## 2. Tauri Stronghold v2 deep dive

The Stronghold integration is substantially shipped; this section catalogs the implementation pattern + risks Engineer should know.

### 2.1 Plugin architecture

`tauri-plugin-stronghold = "2"` (declared in `sunfish/apps/desktop/src-tauri/Cargo.toml`). The plugin wraps the [IOTA Stronghold](https://github.com/iotaledger/stronghold.rs) engine — an encrypted vault for secrets with snapshot-based persistence.

**JS-side API (`@tauri-apps/plugin-stronghold`):**
- `Stronghold.load(path, password)` — opens existing snapshot or creates new
- `stronghold.loadClient(name)` / `createClient(name)` — gets a client handle (named compartment)
- `client.getStore()` — returns the key-value store within the client
- `store.insert(key, bytes)` / `get(key)` / `remove(key)` — KV operations
- `stronghold.save()` — persists current state to the snapshot file

**Rust-side initializer (`src-tauri/src/lib.rs`):** Custom closure passed to `tauri_plugin_stronghold::Builder::with_argon2()` or `with_key_provider()`. Closure receives the JS-supplied password and MUST derive a 32-byte master key from it (or, per Council A1.1, ignore it and derive from OS keychain).

### 2.2 Council A1.1 design (machine-locked master key)

The fleet's design ignores the JS-side password and derives the master key from the OS keychain:

1. On first launch, generate a 32-byte random master key (`getrandom v0.3`).
2. Store the master key in the OS keychain via `keyring v3` with platform-native backends:
   - Windows: Credential Manager / DPAPI
   - macOS / iOS: Keychain
   - Linux: Secret Service (libsecret) via `sync-secret-service` feature
3. On subsequent launches, retrieve the master key from the OS keychain.
4. Use the master key (32 bytes) to decrypt the Stronghold snapshot.

**Why this design:**
- **No JS-side secret.** The Stronghold password from JS is unused; the master key never leaves the Rust process (and never touches disk in unencrypted form).
- **Machine-locked.** Stealing the snapshot file alone yields nothing (no master key); the attacker also needs OS-keychain access.
- **Standard Council A1.1 hardening pattern.**

### 2.3 Known risks (Engineer + po-mac focus)

| Risk | Mitigation in PR 1 scope? | Notes |
|---|---|---|
| OS keychain unavailable (fresh Linux without libsecret) | Partial — keyring v3 errors visible; UX flow not yet defined | Graceful-degradation UX needed; hand-off doesn't specify |
| Snapshot file corrupted | NOT in PR 1 scope | Recovery path: detect failed Stronghold.load → prompt user → clear snapshot → restart login |
| OS keychain entry cleared (user cleared keychain) | NOT in PR 1 scope | Same recovery path as snapshot corruption (snapshot becomes unreadable) |
| Multi-process snapshot lock | Partial — `tauri-plugin-single-instance` enforces single Tauri process | Test recovery from a crashed prior instance holding the lock |
| Token rotation (refresh tokens) | Not in PR 1 scope | Bridge auth flow currently single-token; refresh-token support is future |

### 2.4 Bridge `/auth/login?redirect=tauri://localhost` flow

PR 1 hand-off step 4: "First-launch UI: if no token in Stronghold, redirect to Bridge login flow."

**Open work:**
- Verify Bridge has `tauri://localhost` in the allowed-redirects list (Bridge's auth endpoints currently target browser-domain redirects).
- The Tauri webview handles the redirect; the token is delivered via a query parameter (`?token=...`) OR via a custom Tauri URL scheme that Tauri intercepts.
- Stronghold.setToken() captures the token; subsequent app loads skip the login redirect.

**Risk:** if Bridge `/auth/login` is a Razor-rendered page rather than an API endpoint, the Tauri webview displays the Bridge login UI inside Tauri — acceptable, but the UX should be checked (does the Tauri webview window look right; is the "back" navigation handled).

---

## 3. Bridge multi-role authorization research

### 3.1 Current state

`signal-bridge/Sunfish.Bridge/Authorization/`:
- `AuthenticatedTenantPolicy` — single policy, `RequireAuthenticatedUser()`; no role binding
- `Sunfish.Bridge.Cockpit.CockpitEndpoints.CockpitPolicyName` — requires `role ∈ {owner, spouse}` (existing precedent for role-scoped policy)

`Sunfish.Bridge.Data.Authorization.Roles`:
- `Manager` (existing constant; used by DemoTenantContext)
- Other roles TBD per source file inspection (not exhaustively checked in this research)

### 3.2 Proposed PR 2 policy surface

Per §1 PR 2 above (ONR recommendation: Option A — one policy per role):

```csharp
// signal-bridge/Sunfish.Bridge/Authorization/AccountantPolicy.cs
public static class AccountantPolicy
{
    public const string PolicyName = "AccountantPolicy";
    public static AuthorizationOptions AddAccountantPolicy(this AuthorizationOptions options)
    {
        options.AddPolicy(PolicyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("role", Sunfish.Bridge.Data.Authorization.Roles.Accountant);
        });
        return options;
    }
}

// signal-bridge/Sunfish.Bridge/Authorization/CpaPolicy.cs (similar)
// signal-bridge/Sunfish.Bridge/Authorization/TenantPortalPolicy.cs (similar; subject to magic-link JWT validation)
```

### 3.3 Roles constant additions

```csharp
// signal-bridge/Sunfish.Bridge.Data/Authorization/Roles.cs
public static class Roles
{
    public const string Manager = "manager";        // existing
    public const string Owner = "owner";            // existing
    public const string Spouse = "spouse";          // existing
    public const string Accountant = "accountant";  // P4 PR 2 new
    public const string Cpa = "cpa";                // P4 PR 3 new
    public const string Tenant = "tenant";          // P4 PR 3 new
}
```

### 3.4 ITenantContext extension

Current `ITenantContext.HasPermission(string permission)` returns `bool`. The demo seam returns `true` for all permissions. Phase 4 PR 2 needs a real check:

```csharp
// Possible impl in a new ClaimsBackedTenantContext (replacing DemoTenantContext eventually)
public bool HasPermission(string permission)
{
    return _claims.Any(c => c.Type == "permission" && c.Value == permission);
}
```

But: ADR 0091 R2 deferred the `HasPermission` evolution to claims-based (`AuthorizationResult` / `IAuthorizationRequirement` shape per ASP.NET Core). For P4 PR 2's interim, `bool HasPermission(string)` stays — Phase 4 is NOT the right time to evolve the surface (cross-cutting evolution; out of P4 scope per ADR 0091 R2 O-1 deferral).

### 3.5 Open questions for .NET-architect council (filed via inbox after this research)

1. **Option A (per-role policy class) vs Option B (single claims-based fan-out)** — sanity check ONR's recommendation of Option A.
2. **`DemoTenantContext` evolution** — Path A (seed multiple demo users) vs Path B (multi-role config); ONR recommends Path A.
3. **Permission evolution timing** — `bool HasPermission(string)` stays through P4; ADR 0091 R2 deferred to production OIDC-impl ADR. Confirm.

### 3.6 Open questions for security-engineering council

1. **Demo seam coexistence** — can `DemoTenantContext` coexist with a claims-backed seam (development-only feature flag), OR must it be gone before P4 PR 2 ships?
2. **Magic-link JWT signing** — Bridge currently has `IOperationSigner` (per W#18 + ADR 0046). Same signer for magic-link tokens, OR separate key with rotation policy?
3. **Tenant portal CORS** — if deployed as Bridge static (recommended), CORS is moot. If deployed separately, need a CORS allowlist + CSP design.

---

## 4. Magic-link contract research (W#18 precedent + P4 specifics)

### 4.1 W#18 Phase 5 precedent

`Sunfish.Kernel.Audit` has:
- `AuditEventType.VendorMagicLinkIssued`
- `AuditEventType.VendorMagicLinkConsumed`

The vendor flow (W#18) uses a single-use JWT scoped to a vendor's email; consumption flips the audit state. Cohort-3 + P4 tenant portal mirrors this exactly.

### 4.2 P4 magic-link surface additions

```csharp
// Sunfish.Kernel.Audit.AuditEventType (additions)
TenantMagicLinkIssued        = "Tenant.MagicLinkIssued",
TenantMagicLinkConsumed      = "Tenant.MagicLinkConsumed",
TenantMagicLinkExpired       = "Tenant.MagicLinkExpired",   // emitted on attempted use of expired token
TenantMagicLinkRevoked       = "Tenant.MagicLinkRevoked",   // future; if CO revokes a token before expiry

// Future cohort: CpaMagicLinkIssued + CpaMagicLinkConsumed (P4 PR 3 may include if CPA uses magic-link auth)
```

### 4.3 JWT shape

```json
{
  "iss": "sunfish-bridge",
  "sub": "tenant:<tenant-row-id>",
  "aud": "tenant-portal",
  "tenant_id": "<co-tenant-id>",
  "iat": 1716200000,
  "exp": 1716286400,
  "jti": "<token-guid>",
  "audience": "tenant"
}
```

- `sub` identifies the magic-link subject (the tenant accessing data)
- `tenant_id` identifies the data scope (CO's tenant — the org whose data is being read)
- `aud` distinguishes vendor vs tenant vs cpa flows
- `jti` ties to the in-memory token store for single-use enforcement
- `exp` = `iat + 86400` (24h) per hand-off
- Signed with `IOperationSigner` (same key as other Sunfish JWTs; key-rotation policy TBD per sec-eng open question)

### 4.4 Token store contract

```csharp
public interface IMagicLinkTokenStore
{
    Task<MagicLinkToken> IssueAsync(MagicLinkAudience audience, string subjectId, string tenantContextId, TimeSpan ttl, CancellationToken ct);
    Task<MagicLinkToken?> ConsumeAsync(Guid jti, CancellationToken ct);  // returns null if not found, expired, or already consumed
    Task RevokeAsync(Guid jti, CancellationToken ct);                    // future
}

// v1 in-memory impl: Dictionary<Guid, MagicLinkToken> + IHostedService for eviction
// production impl: SQLite (single-instance Bridge) OR Redis (horizontal Bridge)
```

### 4.5 Delivery layer

`blocks-crew-comms` channel-multiplexed gateway (per ADR 0052) sends the link via SMS / email. Fallback per hand-off: display in CO's dashboard if no provider configured.

---

## 5. Bank CSV ingest format research

Per §1 PR 4 above. Detailed format inventory:

### 5.1 Chase CSV

Headers: `Details, Posting Date, Description, Amount, Type, Balance, Check or Slip #`
- `Details`: "DEBIT" / "CREDIT" / "DSLIP" (check slip)
- `Amount`: signed (negative = debit; positive = credit)
- Type: "ACH_CREDIT", "ACH_DEBIT", "FEE_TRANSACTION", "WIRE_OUTGOING", etc.

### 5.2 Bank of America CSV

Headers: `Status, Date, Original Description, Description, Comments, Check Number, Amount, Type, Balance`
- `Amount`: signed
- Status: "CLEARED" / "PENDING"

### 5.3 Wells Fargo CSV

Headers: `Date, Amount, *, *, Description` (some columns are placeholder asterisks; quoting is aggressive)
- Wells Fargo's CSV format is notoriously inconsistent across account types (checking vs credit card vs business)

### 5.4 Capital One CSV

Headers: `Transaction Date, Posted Date, Card No., Description, Category, Debit, Credit`
- Separate Debit + Credit columns (one is always empty)

### 5.5 Generic QuickBooks-friendly CSV

Headers: `Date, Description, Amount, Payee, Account`
- The fleet's ideal target shape (closest to ERPNext Journal Entry)

### 5.6 Implementation pattern

**Column-mapping schema (localStorage v1):**

```typescript
interface BankCsvMapping {
  bankName: string;                    // user-supplied; for selection
  columnMappings: {
    date: string;                      // CSV header for date column
    dateFormat: 'MM/DD/YYYY' | 'YYYY-MM-DD' | 'DD/MM/YYYY';
    amountMode: 'signed' | 'separate-debit-credit';
    amountColumn?: string;             // when amountMode === 'signed'
    debitColumn?: string;              // when amountMode === 'separate-debit-credit'
    creditColumn?: string;
    description: string;
    referenceColumn?: string;
  };
  encoding: 'utf-8' | 'windows-1252' | 'latin-1';
  delimiter: ',' | '\t' | ';';
}
```

**Persistence:** `localStorage.setItem('bankCsvMappings', JSON.stringify(mappings))` — array of mappings; user selects one at import time OR creates new mapping if bank not seen before.

### 5.7 Duplicate detection

Per hand-off ("warn, don't block"):

- Compute `dedupHash = SHA-256(date + ":" + amount + ":" + reference)` per imported row
- Query ERPNext: any existing Journal Entry with the same hash in the past 30 days?
- If yes, surface in UI as "Possible duplicate (existing entry from 2026-04-15)" — user can override + import anyway, OR skip
- Algorithm conservative: matches on EXACTLY (date, amount, reference); near-matches not detected (acceptable for v1)

### 5.8 CSV parser choice

**Frontend (JS):** `papaparse` v5+ (well-maintained; handles RFC 4180 quoting + multiple delimiters + encoding detection).

**Backend (.NET):** `CsvHelper` v33+ (mature; strong typing; handles all the edge cases of bank exports).

If consistency between front- and back-end parsing matters (and it does — frontend previews + backend posts), the backend should re-parse the CSV after upload and compare row counts; warn if mismatch.

---

## 6. Phase 5 (peer-sync) future-note — NOT P4 scope

Recorded here so Engineer + po-mac + po-win don't accidentally implement peer-sync inside P4.

### 6.1 Phase 5 architectural ancestors

- **ADR 0061 — Headscale Mesh VPN.** Defines the mesh-VPN substrate for inter-node auth + sync.
- **ADR 0086 — Anchor Tauri React Product Surface.** Defines the Tauri shell architecture; Phase 5 extends to peer-node.
- **Paper §13 — Local-first software.** The intellectual ancestor of the peer-sync design.

### 6.2 Phase 5 expected components

| Component | Where it lives | Phase 5 PR? |
|---|---|---|
| Accountant Anchor install | `apps/anchor-tauri/` (extended for non-CO users) | PR A |
| Headscale device registration | New `sunfish/apps/desktop/src-tauri/src/mesh/` module | PR B |
| Sibling sync over mesh | Sync engine extension to multi-node | PR C |
| SQLite + Loro CRDT extension to financial entities | `apps/anchor-tauri/src-tauri/src/db.rs` + CRDT layer | PR D |
| Conflict resolution UX | New `apps/anchor-tauri/src/pages/SyncConflicts.tsx` | PR E |

### 6.3 Phase 5 ADR (future)

A dedicated ADR will design Phase 5: title likely `ADR 0XXX — W#60 Phase 5 Local-First Peer Sync`. Scope:
- Conflict resolution semantics (Loro CRDT for AP-class; manual review for financial entities)
- Mesh device naming + registration flow
- Authentication model (mTLS over Headscale OR JWT-over-HTTPS-tunneled-through-mesh)
- Sync protocol (push-pull intervals; eventual consistency window)

**ONR's research queue item #5 (production OIDC-impl ADR scoping) may overlap with Phase 5 auth model.** Cross-reference will be flagged when item #5 research lands.

---

## 7. Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| OS keychain unavailable on fresh Linux | Medium | High (Stronghold unusable) | UX fallback flow ("install libsecret"); document |
| Snapshot corruption / keychain entry cleared | Low | High (user locked out) | Recovery flow: detect → clear → re-login |
| Multi-role policy design fragmentation | Medium | Medium (refactor cost) | Open question to .NET-arch council before PR 2 |
| Magic-link token leakage in logs / URLs | Medium | High (account takeover) | JWT in URL fragment (`#token=`) NOT query string; sec-eng review |
| Bank CSV format drift (banks change exports) | High | Low (user re-maps) | Localized mapping; per-bank presets future |
| ERPNext Frappe v15 compatibility (MariaDB) | Medium | Medium (deployment blocker) | Pin `mariadb:10.6+` in compose; document |
| Tenant portal CORS misconfiguration | Low | Medium (XSS surface) | If Option A (Bridge static), CORS moot; if Option B/C, sec-eng review |
| Phase 5 peer-sync accidentally implemented in P4 | Medium (humans drift) | High (scope creep) | This research doc + amendment 2026-05-20T03:50Z make the boundary explicit |
| Demo seam coexists indefinitely with claims-backed seam | Medium | Medium (audit fog) | Sec-eng open question to scope demo-seam removal timing |

---

## 8. Open questions

For Admiral routing (file `onr-question-*` per fleet-conventions):

### For .NET-architect council

1. **Role policy design — Option A (one class per role) vs Option B (single claims-based fan-out)?** ONR recommends Option A; council attests or amends.
2. **`DemoTenantContext` evolution path during Phase 4 — Path A (seed multiple demo users with distinct roles) vs Path B (multi-role config on single demo user)?** ONR recommends Path A.
3. **Tenant portal deployment shape — Option A (Bridge static) vs Option B (separate Vercel/Netlify) vs Option C (Anchor proxy)?** ONR recommends Option A.

### For security-engineering council

1. **Demo seam removal timing — can `DemoTenantContext` coexist with a claims-backed seam (development-only feature flag) through Phase 4, OR must it be removed before PR 2 ships?**
2. **Magic-link JWT — signed with existing `IOperationSigner` key, OR new key with rotation policy?**
3. **Magic-link delivery — URL fragment (`#token=`) recommended over query string for log-safety; confirm or amend.**
4. **Tenant portal CORS / CSP shape — if Option A (Bridge static), is the same Bridge CSP sufficient, OR does the tenant portal need a more restrictive CSP (no cookies; no Bridge auth tokens)?**

### For CIC

1. **AP Aging cartridge ship timing — does Phase 4 PR 4 (Bank CSV) take priority over kicking off AP Aging cartridge work (which would unblock cohort-4 ApAgingPage)?** Out-of-scope for this research; flagging for awareness.
2. **Phase 5 ADR authoring — after Phase 4 PASS, OR queued in parallel with Phase 4 PRs 3-5?** ONR recommends after Phase 4 PASS (avoids context-switching).

---

## 9. Sources cited

### Primary sources

1. `shipyard/icm/_state/handoffs/w60-collaboration-phase4-stage06-handoff.md` — canonical Phase 4 spec; verified 2026-05-20T11:55Z.
2. `shipyard/docs/adrs/0091-itenantcontext-divergence-resolution.md` (Accepted Rev 2; promoted 2026-05-19T02:40Z) — Bridge `ITenantContext` shape; ADR §"Production OIDC-impl ADR (future)" pre-stages Phase 4 claims-backed evolution.
3. `shipyard/docs/adrs/0061-headscale-mesh-vpn.md` — Headscale mesh substrate; Phase 5 ancestor; not P4 scope.
4. `shipyard/docs/adrs/0086-anchor-tauri-react-product-surface.md` — Tauri shell architecture; Phase 5 substrate.
5. `shipyard/docs/adrs/0088-multiarch-windows-installer-packaging.md` — Windows arch-detection per PR 5; C-now / B-deferred / A-skip.
6. `sunfish/apps/desktop/src-tauri/Cargo.toml` — `tauri-plugin-stronghold = "2"` + `keyring = "3"` with platform-native backends; verified 2026-05-20T11:55Z.
7. `sunfish/apps/desktop/src/services/credentialStore.ts` (98 lines) — current PR 1 implementation; verified 2026-05-20T11:55Z.
8. `signal-bridge/Sunfish.Bridge/Authorization/AuthenticatedTenantPolicy.cs` (45 lines) — current single-policy state; verified 2026-05-20T11:55Z.
9. `signal-bridge/Sunfish.Bridge/Authorization/DemoTenantContext.cs` (75 lines) — current demo seam; verified 2026-05-20T11:55Z.

### Secondary sources

10. `coordination/inbox/admiral-directive-2026-05-19T22-50Z-onr-research-queue-batch-dispatch.md` — parent directive (Item #2).
11. `coordination/inbox/admiral-directive-amendment-2026-05-20T03-50Z-onr-research-queue-item-2-w60-p4-canonical-shape.md` — directive amendment correcting Phase 4 misframing.

### Tertiary sources (referenced but not primary)

12. Tauri Stronghold v2 plugin documentation (current as of plugin v2; retrieved via Cargo.toml dependency).
13. IOTA Stronghold engine docs (transitive via tauri-plugin-stronghold).
14. ASP.NET Core authorization patterns (referenced for Option A / Option B framing).
15. W#18 Phase 5 `kernel-sync` — `VendorMagicLinkIssued` / `VendorMagicLinkConsumed` audit event precedent (cited but not re-verified inline; trust the audit-event-type catalog).
16. ADR 0052 — channel-multiplexed `IOutboundMessageGateway` (magic-link delivery layer).
17. ADR 0046 — `IOperationSigner` (Ed25519 signing primitive).

### External

18. Common US bank CSV export format inventory (Chase / BofA / Wells Fargo / Capital One / QuickBooks-friendly) — based on public documentation + community-curated mapping guides. Detailed inline.
19. ERPNext Frappe v15 + MariaDB compatibility matrix — `mariadb:10.6+` requirement noted; verify against current ERPNext docs at deployment time.
20. `papaparse` v5 (CSV parser) + `CsvHelper` v33 (.NET CSV parser) — recommended parsers; mature and well-maintained.

---

## 10. What ONR does next

Returns to research queue. Per proceed-continuously discipline:

- Item #2 deliverable complete (this doc).
- File `onr-status-*-research-queue-item-2-w60-p4-research-complete.md` + open questions referenced above as separate `onr-question-*` beacons (one beacon per council audience: .NET-architect, security-engineering, CIC — Admiral routes).
- Proceed to Item #3: ADR 0091 Step 2.0 implementation pre-research.

— ONR, 2026-05-20T11:55Z
