# Onboarding-ladder — sub-cohorts Stage-05 scaffold shells

**Authored by:** ONR (V9 batch item #4)
**Requester:** Admiral (per `admiral-directive-2026-05-22T15-35Z` item V9 #4; follow-up to V8 #3)
**Authored at:** 2026-05-22T16-00Z
**Workstream:** TBD (per V8 #3 §7 decision #9 — pending Admiral cohort numbering allocation)

---

## Purpose

V8 #3 (shipyard#117) proposed onboarding-ladder Stage-02 architecture scoping with
5 sub-cohort decomposition. V9 #4 pre-drafts the **canonical scaffold shells** for
each sub-cohort's eventual Stage-05 hand-off — just the canonical structure with
header sections + halt-conditions + adversarial-brief slots, NOT the full hand-offs.

**Why scaffold now (V9 #4 vs full Stage-05 hand-off):**

- Admiral will ratify V8 #3's 10 decisions in a separate ruling beacon; Stage-05
  hand-off authoring blocks on that ratification
- Scaffolding the shells now reduces full Stage-05 authoring time from ~3-5 days
  per sub-cohort to ~1-2 days
- Sub-cohort 1 (substrate) must ship first; pre-scaffolding lets ONR pick up
  authoring within hours of Admiral ratification

**Pre-conditions:**

- V8 #3 decisions resolved by Admiral (NOT YET — pending)
- ADR 0095 (Bootstrap Context) authored OR rejected → Admiral
- ADR 0096 (Email Dispatch Substrate) authored OR rejected → Admiral
- Email provider chosen → CIC
- CAPTCHA provider chosen → CIC
- Cohort numbering (W#NN through W#MM) allocated → Admiral

When these resolve, each shell expands into a full Stage-05 hand-off per cohort-3 /
cohort-4 hand-off precedent (cohort-3 = 841 lines; cohort-4 = 659 lines).

---

## Shell 1 — Sub-cohort 1: Foundational substrate

**Workstream:** W#NN (TBD)
**Estimated effort:** 1 cohort-cycle (~3-4 weeks)
**Lead engineer:** Engineer (with FED + po-mac/po-win support)
**Council requirements:** sec-eng-council (mandatory) + .NET-architect (mandatory) +
test-eng-council (recommended — new substrate)

### 1.1 Context

Foundational substrate for the onboarding-ladder. Net-new substrate cluster:
email dispatch, rate-limit middleware, CAPTCHA integration, bootstrap context.

Per V8 #3 §5.1: **all subsequent sub-cohorts (2-4) depend on sub-cohort 1 substrate**.
This is the critical path.

### 1.2 What ships (high-level)

- New `packages/foundation-email/` package — `IEmailDispatcher.SendAsync` substrate
- AspNetCore rate-limit middleware (per V8 #3 §5.1 — built-in recommended; ratification pending)
- CAPTCHA integration in Bridge layer (Cloudflare Turnstile per V8 #3 §7 #5 — ratification pending)
- `IBootstrapContext` interface (per V8 #3 §2.1 — pre-tenant signup window)
- Email provider integration adapter (provider per V8 #3 §7 #4 — ratification pending)

### 1.3 What sub-cohort 1 does NOT ship

- Tenant aggregate (sub-cohort 2)
- User aggregate (sub-cohort 2)
- Any `/api/signup/*` endpoints (sub-cohort 2)
- Any `/api/invitations/*` endpoints (sub-cohort 4)
- Frontend signup pages (sub-cohort 2)

### 1.4 ADR dependencies

- ADR 0095 — Onboarding Bootstrap Context (NEW; per V8 #3 §2.1) — **MUST BE ACCEPTED BEFORE STAGE-05**
- ADR 0096 — Email Dispatch Substrate (NEW; per V8 #3 §3.1) — **MUST BE ACCEPTED BEFORE STAGE-05**
- ADR 0091 R2 (consumed; consumer narrowing pattern; new bootstrap consumer joins)
- ADR 0092 (consumed; new repository pattern; no amendment needed for sub-cohort 1)

### 1.5 Adversarial Brief (Stage-05 R3 protocol — 8-bullet)

When fully authored, the adversarial brief should cover:

- [ ] Decision: rate-limit storage backend (in-memory vs Redis vs distributed cache); worst case "single-host horizontal scaling vs. multi-host rate-limit fragmentation"
- [ ] Decision: CAPTCHA bypass for development environment; worst case "production deployment with bypass flag enabled"
- [ ] Decision: Email send failure handling (retry vs DLQ vs fail-the-operation); worst case "tenant signup succeeds but user never receives welcome email"
- [ ] Decision: Bootstrap context lifetime (per-request vs scoped); worst case "request boundary leak across pre-tenant + post-tenant phases"
- [ ] Decision: IEmailDispatcher provider rotation; worst case "vendor change forces every consumer to refactor"
- [ ] Decision: Rate-limit identity key (IP vs user-agent vs session); worst case "behind-proxy NAT makes IP-based rate-limit useless"
- [ ] Decision: CAPTCHA failure UX (silent block vs explanatory error); worst case "user blocked with no understanding"
- [ ] Decision: Email rendering format (HTML + text fallback vs HTML-only); worst case "spam-filtering rejection on HTML-only"

### 1.6 PR decomposition (preview; finalize at Stage-05)

| PR | Substrate | Owner | Estimated LOC |
|---|---|---|---|
| PR 1 | `packages/foundation-email/` skeleton + interface | Engineer | ~300 |
| PR 2 | Email provider adapter (chosen provider) | Engineer | ~400 |
| PR 3 | Rate-limit middleware integration | Engineer | ~200 |
| PR 4 | CAPTCHA integration (Bridge layer) | Engineer + FED | ~250 |
| PR 5 | `IBootstrapContext` interface + DI registration | Engineer | ~150 |
| PR 6 | Integration test harness (cross-substrate) | Engineer | ~400 |

**Estimated cumulative LOC:** ~1,700 across 6 PRs.

### 1.7 Halt conditions

- H1: Email provider selection blocked → ONR routes to CIC; sub-cohort 1 cannot ship without
- H2: CAPTCHA provider selection blocked → ONR routes to CIC; can defer to sub-cohort 2 if needed
- H3: ADR 0095 / 0096 rejected → Admiral rescopes (e.g., fold into ADR 0091 / new ADR)
- H4: Substrate breaks .NET-architect council attest → fold cycle expected; ~1-2 weeks recovery
- H5: Existing audit substrate (ADR 0049 / 0094) needs new event type for bootstrap-phase audits → minor amendment PR

### 1.8 Forward-watched concerns

- Email queue back-pressure (if email provider has rate limits the application doesn't observe)
- Email template versioning (where do welcome-email templates live? config vs. code?)
- Rate-limit telemetry (observability — Prometheus / Application Insights export?)
- CAPTCHA accessibility compliance (WCAG; some CAPTCHAs are inaccessible)

---

## Shell 2 — Sub-cohort 2: Public signup + email verification (Surface A + B)

**Workstream:** W#NN+1 (TBD; depends on sub-cohort 1 numbering)
**Estimated effort:** 1 cohort-cycle (~3 weeks)
**Lead engineer:** Engineer + FED (paired-cohort)
**Council requirements:** sec-eng-council (MANDATORY; first public anonymous surface) +
.NET-architect (MANDATORY; new aggregates) + frontend-architect (MANDATORY; new public UX)

### 2.1 Context

First externally-reachable Sunfish endpoint family. Public anonymous endpoint with
antiforgery + rate-limit + CAPTCHA. Creates first Tenant + admin User per the
self-tenant initial-write pattern.

### 2.2 What ships

- `Tenant` aggregate + `ITenantRepository` (self-tenant initial-write pattern)
- `User` aggregate + `IUserRepository` (admin-role variant for signup; tenant-keyed)
- `POST /api/signup` endpoint (anonymous; antiforgery; rate-limited; CAPTCHA)
- `GET /api/signup/check-email-available` endpoint (anonymous; rate-limited)
- `GET /api/signup/verify-email/{signed-token}` endpoint (token-auth; rate-limited per token)
- Welcome email template + send-on-signup pipeline (consumes sub-cohort 1 IEmailDispatcher)
- 2 NEW audit event types: `Security.TenantCreated`, `Security.UserEmailVerified`
- Frontend: `/signup`, `/signup/verify-email`, `/signup/welcome`

### 2.3 What sub-cohort 2 does NOT ship

- First-property wizard (sub-cohort 3)
- Invitations (sub-cohort 4)
- Multi-tenant federation (out of MVP scope)
- Password reset (forward-watch; sub-cohort 5+ or post-MVP)
- 2FA / passwordless (post-MVP)

### 2.4 ADR dependencies

- ADR 0095 (consumed)
- ADR 0096 (consumed)
- ADR 0091 R2 (consumed; first Tenant-aggregate consumer joins narrowed-MultiTenancy variant)
- ADR 0092 (consumed; self-tenant initial-write — pattern-candidate `pattern-self-tenant-initial-write` per V8 #3 §4)
- ADR 0049 (extend AuditEventType enum)
- ADR 0094 (extend reader contracts for new event types)

### 2.5 Adversarial Brief (Stage-05 R3 protocol — 8-bullet)

When fully authored, the adversarial brief should cover:

- [ ] Decision: signup email format (validate Acme RFC 5321 vs. permissive); worst case "valid edge-case email rejected"
- [ ] Decision: password strength policy (NIST SP 800-63 vs. complexity rules); worst case "weak password policy lets attacker brute-force in production"
- [ ] Decision: tenant-name uniqueness scope (global vs. case-insensitive vs. similarity-checked); worst case "two tenants 'Acme' and 'ACME' confuse end-users"
- [ ] Decision: welcome-email link TTL (1h vs 24h vs 7d); worst case "user signed up and didn't verify same day; link expired and account stuck"
- [ ] Decision: email-verification before-or-after first login; worst case "unverified account creates Property data; abandons; ghost data"
- [ ] Decision: tenant-name → subdomain (slug derivation); worst case "valid tenant name produces invalid subdomain"
- [ ] Decision: self-tenant initial-write permission scope (Tenant aggregate only vs. broader); worst case "future aggregate accidentally inherits permission"
- [ ] Decision: signup form rate-limit threshold (10/IP/min vs. 100/IP/hr); worst case "legitimate small office behind NAT blocked"

### 2.6 PR decomposition (preview; finalize at Stage-05)

| PR | Substrate | Owner | Estimated LOC |
|---|---|---|---|
| PR 1 | `Tenant` aggregate + repository | Engineer | ~400 |
| PR 2 | `User` aggregate (admin role) + repository | Engineer | ~350 |
| PR 3 | `POST /api/signup` Bridge endpoint | Engineer | ~450 |
| PR 4 | Email verification endpoint + token signing | Engineer | ~300 |
| PR 5 | Frontend signup pages | FED | ~600 |
| PR 6 | Welcome email template + pipeline | Engineer + FED | ~250 |
| PR 7 | Audit event types + integration | Engineer | ~150 |
| PR 8 | End-to-end integration tests | Engineer + FED | ~400 |

**Estimated cumulative LOC:** ~2,900 across 8 PRs.

### 2.7 Halt conditions

- H1: Sub-cohort 1 substrate not yet shipped → ONR holds Stage-05 authoring
- H2: Self-tenant initial-write pattern attestation fails .NET-architect → fold cycle (~1 week)
- H3: Frontend signup design QA fails PAO/Yeoman → UX iteration (~3-5 days)
- H4: Welcome email rendering breaks in major email clients → template-revision PR
- H5: CAPTCHA accessibility audit fails → may need provider swap

### 2.8 Forward-watched concerns

- Tenant-name reservation race (two concurrent signups for same tenant-name)
- Email-verification token reuse (single-use enforcement)
- Brute-force on `check-email-available` endpoint (enumeration attack)
- Welcome email deliverability (SPF / DKIM / DMARC setup)
- Account-creation auditing (who signed up what tenant; correlation_id propagation)

---

## Shell 3 — Sub-cohort 3: First-property wizard (Surface C)

**Workstream:** W#NN+2 (TBD)
**Estimated effort:** ~0.5 cohort-cycle (~1.5 weeks)
**Lead engineer:** FED (primarily); Engineer no-op (cohort-1 substrate consumed)
**Council requirements:** frontend-architect (mandatory; UX-heavy) + PAO (design review)

### 3.1 Context

Pure frontend cohort. No backend substrate changes. The "welcome experience" for
post-signup admin users — onboards them to creating their first Property.

### 3.2 What ships

- `/onboarding/wizard` route + Wizard component
- Empty-state UX in PropertiesListPage (no properties → wizard CTA)
- Welcome banner in dashboard (dismissable)
- Microcopy / empty-state strings
- 1-3 wizard steps (TBD; design decision)

### 3.3 What sub-cohort 3 does NOT ship

- New backend endpoints (consumes cohort-1 `/api/properties` directly)
- New audit events
- New substrate types

### 3.4 ADR dependencies

- None new — consumes existing cohort-1 substrate

### 3.5 Adversarial Brief (light; not strictly required per ADR 0093 — but recommended)

- [ ] Decision: wizard exit / dismiss behavior; worst case "user dismisses, never re-opens; never creates property"
- [ ] Decision: wizard step count (1 vs 3 vs 5); worst case "too many steps; abandonment"
- [ ] Decision: wizard skip-to-end vs guided; worst case "user wants to skip, but flow forces them through"
- [ ] Decision: empty-state CTA copy; worst case "ambiguous; user doesn't know what to do"

### 3.6 PR decomposition (preview)

| PR | Substrate | Owner | Estimated LOC |
|---|---|---|---|
| PR 1 | Wizard component + state machine | FED | ~500 |
| PR 2 | Empty-state PropertiesListPage rewrite | FED | ~200 |
| PR 3 | Welcome banner + dismissal state | FED | ~150 |
| PR 4 | End-to-end smoke tests + visual baselines | FED | ~250 |

**Estimated cumulative LOC:** ~1,100 across 4 PRs.

### 3.7 Halt conditions

- H1: Sub-cohort 2 not yet shipped → no user accounts to wizard-onboard
- H2: PAO design QA fails → UX iteration cycle
- H3: Wizard accessibility audit fails (focus management, screen-reader flow) → polish iteration

### 3.8 Forward-watched concerns

- Wizard re-open after dismissal (user wants help later)
- Wizard analytics (which steps lose users?)
- Wizard localization (when i18n foundation lands per cohort-4 Nit 6 forward-watch)

---

## Shell 4 — Sub-cohort 4: Invitations (Surface D)

**Workstream:** W#NN+3 (TBD)
**Estimated effort:** 1 cohort-cycle (~3 weeks)
**Lead engineer:** Engineer + FED (paired-cohort)
**Council requirements:** sec-eng-council (MANDATORY; token-auth + cross-tenant invitation
isolation) + .NET-architect (MANDATORY; new aggregate) + frontend-architect (recommended)

### 4.1 Context

Admin user can invite additional team members. Token-authenticated registration
flow; tenant-keyed invitation aggregate; email-delivered links.

### 4.2 What ships

- `Invitation` aggregate + `IInvitationRepository` (NEW aggregate; tenant-keyed)
- `POST /api/invitations` endpoint (admin-auth; antiforgery; idempotency-key)
- `GET /api/invitations/{id}` endpoint (admin-auth; list/detail)
- `DELETE /api/invitations/{id}` endpoint (admin-auth; revoke)
- `GET /api/invitations/accept/{signed-token}` (anonymous; token-auth)
- `POST /api/invitations/accept/{signed-token}` (anonymous; token-auth; password setup)
- Invitation email template + send-on-create pipeline
- Background sweep (`IHostedService`) for invitation expiration
- 5 NEW audit event types: `Security.InvitationSent`, `Security.InvitationAccepted`,
  `Security.InvitationExpired`, `Security.InvitationRevoked`, `Security.UserRegistered`
- Frontend: admin invitations management UX + `/invitations/accept` page

### 4.3 What sub-cohort 4 does NOT ship

- 2FA / passwordless on invitation accept (post-MVP)
- Bulk invitation (e.g., CSV upload) — forward-watch
- Invitation analytics (forward-watch)

### 4.4 ADR dependencies

- ADR 0095 + 0096 (consumed)
- ADR 0091 R2 (consumed; first Invitation-aggregate consumer)
- ADR 0092 (consumed; new tenant-keyed aggregate)
- ADR 0046 (consumed; invitation-token signing)
- ADR 0049 + 0094 (extend audit event types)
- NEW ADR 0097 (Invitation Aggregate) — OPTIONAL per V8 #3 §4 (could be candidate-pattern only)

### 4.5 Adversarial Brief (Stage-05 R3 protocol — 8-bullet)

When fully authored, the adversarial brief should cover:

- [ ] Decision: invitation TTL (1h vs 24h vs 7d); worst case "stale invitation usable a year later"
- [ ] Decision: invitation single-use enforcement (atomic check-and-mark vs eventual-consistency); worst case "race condition lets invitation register two users"
- [ ] Decision: cross-tenant invitation prevention; worst case "admin tenant A sends invitation; tenant B user accepts; cross-tenant join"
- [ ] Decision: invitation role-elevation; worst case "non-admin invites admin → role-escalation attack"
- [ ] Decision: invitation revocation timing (immediate vs lazy); worst case "revoked invitation still acceptable for several minutes"
- [ ] Decision: invitation token leak surface (email forwarding, screenshot, etc.); worst case "leaked token impersonates inviter's tenant"
- [ ] Decision: invitation accept-with-existing-account; worst case "user already in tenant accepts invitation; joins again or errors"
- [ ] Decision: invitation expiration sweep cadence; worst case "expired invitations linger in DB; query performance degrades"

### 4.6 PR decomposition (preview; finalize at Stage-05)

| PR | Substrate | Owner | Estimated LOC |
|---|---|---|---|
| PR 1 | `Invitation` aggregate + repository | Engineer | ~400 |
| PR 2 | Invitation create + list + revoke endpoints | Engineer | ~500 |
| PR 3 | Invitation accept (anonymous) endpoint family | Engineer | ~400 |
| PR 4 | Invitation email template + pipeline | Engineer + FED | ~300 |
| PR 5 | Background expiration sweep (`IHostedService`) | Engineer | ~250 |
| PR 6 | Frontend admin invitations UX | FED | ~700 |
| PR 7 | Frontend accept-invitation page | FED | ~400 |
| PR 8 | Audit event types + integration | Engineer | ~150 |
| PR 9 | End-to-end integration tests | Engineer + FED | ~500 |

**Estimated cumulative LOC:** ~3,600 across 9 PRs.

### 4.7 Halt conditions

- H1: Sub-cohort 1 not shipped → no email substrate
- H2: Sub-cohort 2 not shipped → no User aggregate to register
- H3: pattern-self-tenant-initial-write attestation fails .NET-architect → fold cycle
- H4: Invitation single-use enforcement fails sec-eng (race condition) → atomic-check redesign
- H5: PAO design QA on admin UX → iteration cycle (~3-5 days)

### 4.8 Forward-watched concerns

- Invitation reminder emails (auto-resend if not accepted in N days)
- Invitation analytics (admin view of "who I invited; accepted vs pending")
- Bulk invitations (CSV upload)
- Invitation role-revocation (if invitation is for a role being deprecated)
- Invitation cross-tenant defense-in-depth (server emits TenantBoundaryViolation if cross-tenant attempt)
- Invitation token storage (hashed vs plaintext in DB — IOperationSigner verifies signature so DB doesn't need plaintext)

---

## Shell 5 — Sub-cohort 5: Polish + integration

**Workstream:** W#NN+4 (TBD)
**Estimated effort:** ~0.5 cohort-cycle (~1.5 weeks)
**Lead engineer:** FED (primarily); QM (seed scripts); Engineer (minor backend support)
**Council requirements:** sec-eng-council (light; covers end-to-end demo dry-run findings)

### 5.1 Context

End-to-end polish for MVP demo. Connects sub-cohorts 1-4 into a coherent user
experience; ensures demo readiness.

### 5.2 What ships

- End-to-end demo dry-run protocol + script
- Demo seed data (50-unit synthetic per V7 #3 + V8 #3 §7 #4 ratification)
- Error-page polish (404 / 500 / 403 — consistent UX)
- Accessibility audit pass (WCAG 2.1 AA; per cohort-4 Nit 4 precedent)
- Performance audit (Lighthouse / Core Web Vitals)
- Documentation: user-facing onboarding guide

### 5.3 What sub-cohort 5 does NOT ship

- New backend endpoints (purely consumes 1-4)
- New audit events
- New ADRs

### 5.4 ADR dependencies

- None new

### 5.5 PR decomposition (preview)

| PR | Substrate | Owner | Estimated LOC |
|---|---|---|---|
| PR 1 | Demo seed data + scripts | Engineer + QM | ~600 |
| PR 2 | Error-page polish | FED | ~300 |
| PR 3 | Accessibility audit + fixes | FED | ~400 |
| PR 4 | Performance audit + fixes | FED | ~200 |
| PR 5 | User-facing onboarding documentation | PAO + Yeoman | ~800 (docs) |

**Estimated cumulative LOC:** ~2,300 (mostly docs + scripts).

### 5.6 Halt conditions

- H1: Sub-cohorts 1-4 not all shipped → demo dry-run can't run
- H2: Performance audit fails (Lighthouse < 80) → optimization cycle
- H3: Accessibility audit reveals blockers → polish iteration

### 5.7 Forward-watched concerns

- Multi-tenant demo (does the demo run for 2+ tenants concurrently? recommended)
- Demo data refresh cadence (how often is seed regenerated?)
- Documentation versioning (does onboarding doc track which features are in which release?)

---

## Sequencing summary

```
Sub-cohort 1 (substrate; 3-4 wk; critical path)
    ├──► Sub-cohort 2 (signup A+B; 3 wk; depends on 1)
    │        ├──► Sub-cohort 3 (wizard C; 1.5 wk; depends on 2)
    │        │
    │        └──► Sub-cohort 4 (invitations D; 3 wk; depends on 1+2)
    │                  └──► Sub-cohort 5 (polish; 1.5 wk; depends on 1+2+3+4)
```

**Critical-path calendar:** 3-4 wk + 3 wk + (max(1.5, 3)) + 1.5 wk = ~9-9.5 weeks
serial; ~5-6 weeks if sub-cohorts 3 + 4 run parallel (per V8 #3 §6 estimate).

---

## Cross-shell forward-watched concerns

Items that span multiple sub-cohorts:

1. **Tenant data backup + restore** — not addressed in any sub-cohort; post-MVP gap
2. **Tenant deletion** — not addressed; post-MVP gap (GDPR compliance angle)
3. **Audit-trail viewing for onboarding events** — cohort-4's audit-trail viewer
   handles this naturally if event types are registered correctly
4. **Multi-tenant federation onboarding** — federated signup (sign in with Google/Microsoft/etc) — post-MVP
5. **Account recovery / password reset** — sub-cohort 5+ candidate; not in MVP
6. **Tenant subdomain provisioning** — DNS / TLS automation for `{tenant}.sunfish.app`
   — depends on hosting decision (post-MVP for self-hosted; embedded in cloud-hosted)
7. **Trial period / billing integration** — depends on commercial model; CIC ratification
   needed; not in onboarding-ladder scope per V8 #3

---

## When to expand shells into full Stage-05 hand-offs

ONR begins full Stage-05 hand-off authoring for each shell when:

1. **Admiral ratifies V8 #3 decisions** (10 items pending)
2. **Sub-cohort 1 ADRs accepted** (0095 + 0096 + any new ADR for self-tenant pattern)
3. **Vendor selections finalized** (email + CAPTCHA + rate-limit substrate)
4. **Workstream numbers allocated** (W#NN through W#NN+4)
5. **Sub-cohort N-1 has shipped Stage-06** (so dependencies are real, not promised)

Per V7 #5 Stage-05 retrospective template, each full Stage-05 hand-off includes:
- Adversarial Brief (8-bullet per ADR 0093 R3 protocol)
- 8-12 forward-watched items
- Halt conditions with specific recovery paths
- PR decomposition with explicit dependencies
- Pattern claims (formal + candidate)
- Decision-routing matrix (Admiral, CIC, councils)

Each shell here provides ~80% of the structural scaffolding for the full hand-off.

---

## Sources cited

1. `coordination/inbox/admiral-directive-2026-05-22T15-35Z` item V9 #4
2. V8 #3 onboarding-ladder Stage-02 scoping (shipyard#117)
3. V7 #3 MVP demo critical-path analysis (shipyard#111)
4. V7 #5 Stage-05 retrospective scaffold (shipyard#110)
5. cohort-3 hand-off precedent (shipyard `icm/_state/handoffs/anchor-react-rebind-cohort-3-stage06-handoff.md`)
6. cohort-4 hand-off precedent (shipyard `icm/_state/handoffs/cohort-4-c3-audit-trail-viewer-stage06-handoff.md`)
7. V9 #1 cohort-4 FED PR-by-PR detail specs (shipyard#119)
8. ADR 0093 R3 Adversarial Review Protocol (shipyard#104 MERGED)

---

## What ONR does next

V9 #4 deliverable complete. V9 batch state:
- V9 #1 PRIMARY done (shipyard#119; cohort-4 FED specs)
- V9 #2 CONDITIONAL — deferred (QM V5 #3 not landed)
- V9 #3 CONDITIONAL — deferred (QM V5 #1 + PAO #116 not at expected state)
- V9 #4 done (this doc)

ONR files V9 partial-complete idle beacon: 2 of 4 V9 items shipped (PRIMARY + light item);
2 conditional items explicitly deferred. Awaits V10 dispatch.

— ONR, 2026-05-22T16:00Z
