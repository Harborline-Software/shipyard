# Shipyard — Launch Positioning One-Pager

**Product:** Shipyard, open-source property-management platform
**License:** MIT
**Parent brand:** Harborline-Software
**UI framework underneath:** Sunfish (the design system / component library)
**Author:** PAO, 2026-05-29
**Status:** Draft — pending Admiral / CIC review
**Companion docs:** `landing-page-copy.md`, `launch-day-messaging.md` (this folder); voice + guardrails inherited from `../shipyard-marketing-copy.md`

> **Voice note (inherited).** Write like you are talking to a landlord who
> manages a dozen units from a kitchen table and is sick of spreadsheets. Say
> what the software does, not how it makes them feel. Banned words: *powerful*,
> *seamless*, *all-in-one*, *revolutionary*, *enterprise-grade*. No claims that
> imply data leaves the operator's control without their say-so.

---

## Elevator line

**Shipyard is open-source property management you run yourself — track leases,
see who owes what, and close your books, without a monthly bill or a vendor
holding your records.**

(Twitter-length variant, 138 chars: *Open-source property management for small landlords. Track leases, see who owes what, close your books. Run it yourself. MIT.*)

---

## Who it's for

- **Primary:** Individual landlords and small property-management firms managing
  roughly **5 to 20 units** — residential, mixed-use, small portfolios.
- **Secondary:** Developers and self-hosters who want a real, running reference
  implementation of a local-first / hosted-node ERP they can read, fork, and
  extend (Shipyard is also the reference app behind the Harborline architecture).

The wedge: operators who have **outgrown spreadsheets** but for whom a per-unit
SaaS subscription feels like *paying rent on their own records*.

---

## Core promise

Get your rentals out of the spreadsheet and into one place you control — leases,
rent, and the books — without signing up for a service that owns your data or
bills you per door, per month, forever.

---

## Differentiators (5)

1. **Open source, MIT, no per-unit bill.** No seats to buy, no plan to upgrade,
   no lock-in. Clone it and run it. *(Verified: LICENSE = MIT; rebrand memo
   confirms all fleet products MIT OSS.)*

2. **You choose where it runs.** The same app supports running on your own
   machine (a Tauri desktop build exists), self-hosting a node for your office,
   or using a hosted node — your records stay exportable to JSON/CSV in every
   mode. *(Verified: bundle manifests declare `deploymentModesSupported:
   ["Lite", "SelfHosted", "HostedSaaS"]`; a Tauri desktop app and an
   Aspire-based `local-node-host` both exist in-repo.)*

3. **Built from composable blocks, not a monolith.** Shipyard is assembled from
   business-case *bundles* (Property Management today; Asset Management and
   Project Management in build) that compose reusable *blocks* — leases, rent
   collection, maintenance, inspections, accounting, tax. You turn on what you
   need. *(Verified: `foundation-catalog/Manifests/Bundles/` holds
   property-management, asset-management, project-management, facility-operations,
   acquisition-underwriting manifests; all currently `status: Draft, maturity:
   Scaffold` — see "Honesty ledger" below.)*

4. **Your data is yours and stays readable.** Tenant-owned data, exportable to
   JSON/CSV in every deployment mode; full data portability is a stated
   requirement for the run-it-yourself modes. The files stay openable whether or
   not the project is around in ten years. *(Verified: `dataOwnership` field on
   every bundle manifest.)*

5. **Multi-tenant from the foundation.** One install can hold more than one
   company's books, kept isolated — useful for a small firm managing buildings
   for several owners. *(Verified: `foundation-multitenancy` package exists and
   the web cockpit ships a working CompanySwitcher / multi-company store.)*

---

## What the MVP actually ships at launch

The launch surface is the **property-management cockpit**: a logged-in operator
lands in a multi-company workspace and works real records. Verified live in the
web app router (`sunfish/apps/web/src/app.tsx`) and backed by native data hooks:

| Capability | Route | Verified |
|---|---|---|
| Properties list + detail | `/properties`, `/cockpit/:propertyId` | Yes — wired to `useProperties` |
| Leases list + detail (active / expiring / expired) | `/leases`, `/leases/:name` | Yes — `useLeases`, expiry logic |
| Rent collection | `/rent` | Yes — `RentCollectionPage` |
| Rent roll report | `/reports/rent-roll` | Yes |
| Accounting overview + Trial Balance + AR aging | `/accounting` | Yes — `useAccounting`, AR-aging page present |
| Profit & Loss (incl. by-property) | `/reports/profit-loss` | Yes |
| Maintenance + work orders + vendors | `/maintenance`, `/cockpit/work-orders`, `/cockpit/vendors` | Yes |
| Crew comms | `/comms` | Yes — `CrewCommsPage` present |
| Audit-event trail | (cohort-4 audit viewer) | Pages present (`AuditEventsPage`) |
| Multi-company switching | CompanySwitcher in header | Yes |

**Property Management is the MVP core. Asset Management and Project Management
bundles are in build** (manifests authored; substrate ADR-0101 for asset-mgmt is
at Rev 1) and should be positioned as *coming*, not *shipping*.

---

## Honesty ledger — claims to confirm before anything goes public

These are the spots where the verified ground truth is thinner than marketing
instinct would like. Admiral / CIC should confirm or trim:

1. **The signup -> email-verify -> login flow is NOT yet merged into the live web
   router.** `app.tsx` has no `/signup`, `/login`, or `/verify-email` routes; it
   redirects `/` straight to `/properties` and the `whoami` call falls back to a
   `dev-user` / `owner` identity. A `CaptchaWidget` component exists (signup
   substrate is staged), and the onboarding-ladder hand-off
   (`icm/_state/handoffs/onboarding-ladder-sub-cohorts-scaffold.md`) specifies
   the full signup/verify/invite surface — but **the public onboarding entry
   point is the launch gate**, consistent with the brief's "gated only on a live
   auth smoke-test." *Do not publish copy that promises a one-click signup until
   that flow is merged and the smoke-test passes.* The landing-page CTA is
   written with a self-hosted fallback so it stays true either way.

2. **All bundle manifests are `status: Draft, maturity: Scaffold`.** The manifests
   describe intended composition (which blocks a bundle pulls in), not a
   certification that every listed block is feature-complete. Property-management
   *cockpit pages* are demonstrably live; the *bundle entitlement/provisioning*
   layer (`blocks-businesscases`) is in-memory/scaffold. Frame bundles as "what
   you can compose," not "fully finished verticals."

3. **"Local-first" needs careful scoping.** The web app has network-status
   awareness (`OfflineBanner` reads `navigator.onLine` and currently says
   "Offline — changes can't save yet"), NOT full offline editing + sync. The
   *desktop* (Tauri) and *self-hosted node* shapes are the honest local-first
   story (run it on your own machine); the *web* app is online-cockpit. Claim
   "runs on your machine / self-hostable," not "works fully offline in the
   browser." (The earlier `shipyard-marketing-copy.md` line "it works whether or
   not you're online" overreaches for the web app — flagged for that doc too.)

4. **Self-hosted vs. hosted tension.** The earlier copy says "not on our servers —
   we don't have any." The launch onboarding flow (signup -> tenant cockpit) is a
   *hosted node*. Both are legitimate inverted-stack deployment shapes, but the
   public story must not say both "we have no servers" AND "sign up for a hosted
   account." Recommended resolution: lead with **open-source + run-it-yourself**;
   present any hosted option as "a hosted node, if you'd rather not run your own"
   — same code, your choice. CIC to ratify whether a Harborline-hosted offering
   is in scope for launch messaging at all.

5. **README is stale.** `shipyard/README.md` still calls the platform "Sunfish"
   and references commercial-add-on licensing — pre-rebrand and pre-MIT-decision.
   Out of scope for this deliverable, but the README hero in
   `shipyard-marketing-copy.md` is the correct replacement; route the README
   swap to Engineer/FED.

---

## Tone guardrails (carry into all launch copy)

- Concrete verbs, no hype. "Track your leases. See who owes what. Close your
  books at month-end."
- Numbers stay concrete: "5 to 20 units," not "any size."
- Never imply the operator's data goes somewhere they didn't choose.
- Two names, never confused: **Shipyard** = the property app; **Sunfish** = the
  UI framework it's built on. Public copy for this product says "Shipyard."
