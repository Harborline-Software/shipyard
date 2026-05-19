# Cohort-2 Design Direction — Financial Cluster

**Workstream:** W#75 (anchor-react-rebind-cohort-2 — financial cluster)
**Status:** Track C design direction (PAO authored; Yeoman wireframes)
**Authored:** 2026-05-19
**PAO:** chris (via PAO session 2026-05-18T18:15Z onward)

This directory holds the design-direction artifacts FED will consume before opening cohort-2 PRs 1-3. It is the **first** cohort design-direction artifact set — cohort-1 shipped 4 simple read-mostly pages and skipped formal Track C (per `active-workstreams.md` line 955).

## Scope

Three Sunfish React pages get rebound from direct-ERPNext to the new `/api/v1/financial/*` Bridge family:

| PR | Page | Endpoint | Direction doc |
|---|---|---|---|
| 1 | `LeaseDetailPage.tsx` (payments section) | `GET /api/v1/financial/payments?leaseId=` | [`01-lease-detail-page-payments.md`](./01-lease-detail-page-payments.md) |
| 2 | `AccountingPage.tsx` | `GET /api/v1/financial/accounting/{summary,outstanding}` | [`02-accounting-page.md`](./02-accounting-page.md) |
| 3 | `RentCollectionPage.tsx` | `POST /api/v1/financial/payments` + CSRF | [`03-rent-collection-page.md`](./03-rent-collection-page.md) |

Plus:

- [`tokens.md`](./tokens.md) — design tokens used + any new financial-cluster additions
- [`component-reuse-audit.md`](./component-reuse-audit.md) — `@sunfish/ui-react` reuse + gap flags
- [`csrf-ux-pattern.md`](./csrf-ux-pattern.md) — canonical CSRF round-trip UX (pattern-010 candidate)
- [`cross-tenant-rejection-ux.md`](./cross-tenant-rejection-ux.md) — generic-error UX with diagnostic-non-leak invariant
- [`states-matrix.md`](./states-matrix.md) — empty / loading / success / error variants per page

## Reference inputs

1. **ONR cohort-2 hand-off** — `shipyard/icm/_state/handoffs/anchor-react-rebind-cohort-2-stage06-handoff.md` (961 lines; scope source-of-truth)
2. **FED scope survey** — `coordination/inbox/fed-status-2026-05-19T0030Z-cohort2-scope-survey.md`
3. **W#68 PR 3 sec-eng verdict** — `coordination/inbox/council-verdict-2026-05-19T00-45Z-security-engineering-w68-pr3-spot-check.md` (cross-tenant rejection UX requirement; diagnostic-non-leak invariant)
4. **Cohort-1 PR 3 CSRF reference** — `sunfish/apps/web/src/api/maintenance.ts` (`getCsrfToken()` flow; canonical `X-XSRF-TOKEN` header convention)
5. **Cohort-1 visual baseline** — `sunfish/apps/web/src/pages/MaintenancePage.tsx` (palette + form patterns + status pill convention)
6. **Framework design docs** — `shipyard/_shared/design/{design-language,tokens-guidelines,component-principles,accessibility,internationalization}.md`

## Standing-pattern alignment

- **pattern-009** (Bridge endpoint + frontend rebind pair) — formal post-cohort-1; applies to all three PRs
- **pattern-010-financial-write-path** — CANDIDATE; first instance on RentCollectionPage (PR 3). Pattern-010 = Bridge POST + CSRF antiforgery + tenant-derived audit emission + cross-tenant rejection without diagnostic leak. Ratifies after the second financial write-path PR ships clean. **Design direction makes the pattern VISIBLE** — see `csrf-ux-pattern.md` + `cross-tenant-rejection-ux.md`.

## Surfaced design questions

The following questions came up during Phase A discovery; surfaced to CIC for ratification or to Engineer/ONR for clarification before PRs land.

### Q1 — CSRF endpoint location convention (Engineer + ONR)

ONR's handoff §3.25 snippet shows `GET /antiforgery/token` returning `RequestVerificationToken`. Cohort-1's shipped `maintenance.ts` uses `GET /api/v1/cockpit/antiforgery-token` returning `X-XSRF-TOKEN`. **These differ.**

**Recommendation:** PR 3 follows cohort-1's canonical pattern with a financial-namespaced endpoint: `GET /api/v1/financial/antiforgery-token` returning `{ token }`, POST uses `X-XSRF-TOKEN` header. Symmetric with cockpit's family-scoped token; consistent with the `/api/v1/<family>/*` URL convention.

**Alternative considered + rejected:** reusing `/api/v1/cockpit/antiforgery-token` across families. Rejected because ASP.NET Core's `IAntiforgery` token cookie binding can vary per endpoint registration; a financial-namespaced token endpoint is safer.

This is a small design-direction decision; will note in `csrf-ux-pattern.md` and FED can confirm during PR 3 implementation. **Not a halt condition** — both options work; PR 3 council will validate.

### Q2 — Currency / direction handling on RentCollectionPage

New `RecordPaymentRequest` DTO (handoff §3.24) requires `currency` (ISO 4217) + `direction` ('Inbound' | 'Outbound'). Current `RentCollectionPage` form exposes neither.

**Recommendation:** keep both fields **programmatic** (not user-facing):
- `direction`: hardcoded to `'Inbound'` — rent collection is intrinsically tenant-to-landlord (inbound to operator). If a future "refund" or "credit issuance" page emerges, that's a separate route with `direction='Outbound'`.
- `currency`: sourced from the selected lease's currency (assume `Lease.currency` field exists or defaults to USD; verify with Engineer during PR 3).

This is design direction, not a halt — confirms in `03-rent-collection-page.md`.

### Q3 — "Verify in ERPNext admin" confirmation copy

Current `RentCollectionPage.tsx:53` confirmation view says "Verify in ERPNext admin that the ledger entry is correct." Cohort-2 moves OFF ERPNext — copy needs replacement.

**Recommendation:** replace with audit-trail visibility text per pattern-010 candidate UX (audit-emission acknowledgment is part of pattern-010's visible signature):

> Payment recorded. An audit-trail entry has been emitted. View the lease's payment history to confirm.

Confirmed in `03-rent-collection-page.md` + `csrf-ux-pattern.md`.

---

## Sequencing

1. PAO authors direction docs (this directory) — **THIS PR**
2. Yeoman renders ASCII wireframes per direction (parallel; under PAO supervision)
3. PAO files `pao-status-*-cohort2-track-c-complete.md`
4. FED reads + proceeds to PR 1 execution when W#75 PR cluster pre-auth ratifies and PR 0 substrate retrofits land

## Acceptance

Per directive `admiral-directive-2026-05-19T04-00Z-pao-cohort2-track-c-design-direction.md`:

1. ✅ Three pages × 5 deliverables (wireframe + tokens + reuse audit + states + rejection UX) committed to `shipyard/_shared/design/cohort-2/`
2. ✅ PAO files `pao-status-*-cohort2-track-c-complete.md` referencing the artifacts
3. ✅ FED can read the artifacts standalone and proceed to PR 1 execution without further PAO clarification

— PAO, 2026-05-19
