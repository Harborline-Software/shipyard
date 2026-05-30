# Shipyard — Landing-Page Copy (Launch)

**Author:** PAO, 2026-05-29
**Status:** Draft — pending Admiral / CIC review
**Scope:** Section-by-section landing-page copy for the launch marketing site.
Every feature claim below maps to a capability verified live in
`sunfish/apps/web/src/app.tsx` + its data hooks, or is flagged in the
positioning one-pager's Honesty ledger. UX/layout direction is included as
inline notes for FED (Yeoman is offline; PAO supplies both copy and direction).

> Builds on `../shipyard-marketing-copy.md` (the README hero + GitHub
> descriptions). This doc is the full landing page; that doc is the repo-facing
> copy. Keep them consistent.

---

## Section 1 — Hero

**Headline:**
> Your rentals. Your books. Your machine.

**Subhead:**
> Shipyard is open-source property management for small landlords — track
> leases, see who owes what, and close your books, without a monthly bill or a
> vendor holding your records.

**Primary CTA:** `Get started` → (see CTA note below)
**Secondary CTA:** `View on GitHub`

> **FED / UX direction.** Hero is text-left, product-screenshot-right on
> desktop; stacked on mobile. The screenshot should be the **Properties list or
> the Property detail cockpit** — a real, populated screen, not an abstract
> illustration. The proof here is "this is a finished tool," so show finished
> UI. Keep the hero to one viewport height; no carousel.
>
> **CTA note (load-bearing — see Honesty ledger #1 + #4).** Until the
> signup -> verify -> login flow is merged and smoke-tested, the primary CTA
> must NOT promise hosted signup. Two safe options:
> - **If launch is self-hosted-only:** Primary = `Get started on GitHub`,
>   Secondary = `See how it works` (anchor to the demo/feature section).
> - **If hosted onboarding is live + ratified:** Primary = `Create your
>   workspace` (→ /signup), Secondary = `Or run it yourself` (→ GitHub).
> FED should wire the primary CTA behind a single config flag so it flips
> cleanly the moment the auth smoke-test passes.

---

## Section 2 — The problem (one short band, sets up the promise)

**Eyebrow:** Sound familiar?

**Body:**
> One spreadsheet for leases, another for rent, a shoebox for receipts, and a
> nagging feeling at tax time that something doesn't add up. The software built
> to fix that usually charges per unit, per month, forever — and keeps your
> records on a server you'll never see.

> **UX direction.** Quiet band, muted background. No icons-in-a-row cliché here;
> this is a single paragraph that earns the feature section. Optional: three
> tiny inline labels ("Leases.xlsx", "Rent_2026.xlsx", "Receipts/") rendered as
> worn spreadsheet tabs to make the pain concrete.

---

## Section 3 — Feature highlights (mapped to shipped capabilities)

> **UX direction.** Six cards, 3×2 grid on desktop, single column mobile. Each
> card: a small Sunfish-token icon, a 2–4 word title, one or two plain
> sentences. No marketing adjectives in the card body — describe the screen.
> Pull the icon set from the existing `compat-lucide` / `compat-heroicons`
> packages so the site matches the app.

**Card 1 — Leases in one place**
> Keep every lease together — tenant, terms, dates, renewals. See at a glance
> which are active, which end soon, and which need attention. *(Live: `/leases`,
> active/expiring/expired status, 60-day expiry surfacing.)*

**Card 2 — Rent roll**
> One screen showing every unit, its rent, and its current tenant. Know what you
> should be collecting this month before you go chasing it. *(Live:
> `/reports/rent-roll`.)*

**Card 3 — See who owes what**
> Accounts-receivable aging sorts unpaid balances by how late they are, so the
> 30-, 60-, and 90-day problems surface on their own. *(Live: AR-aging in the
> accounting section.)*

**Card 4 — Close the books**
> A trial balance, an accounting overview, and profit-and-loss — including P&L by
> property — let you square the month without exporting your data to anyone.
> *(Live: `/accounting`, `/reports/profit-loss`, Trial Balance.)*

**Card 5 — Maintenance & vendors**
> Log work orders, track them to done, and keep your vendors and their jobs in
> one list. *(Live: `/maintenance`, `/cockpit/work-orders`, `/cockpit/vendors`.)*

**Card 6 — A clear audit trail**
> Every change to the books is recorded, so you can answer "who changed this, and
> when?" without guessing. *(Live: audit-event pages present; confirm the viewer
> is reachable from the cockpit nav before claiming "one click away.")*

> **Optional 7th card — only if multi-tenant is part of the launch story:**
> **Manage several owners' books** — One install can hold more than one company's
> records, kept separate. Switch between them from the top bar. *(Live:
> CompanySwitcher + multi-company store.)*

---

## Section 4 — Why open source & run-it-yourself

**Heading:** Your records, your machine, no middleman

**Body:**
> Shipyard runs where you put it — on your own computer, on a node you host for
> the office, or on a hosted node if you'd rather not run your own. Whichever you
> pick, the data is yours: exportable to plain JSON and CSV any time, and
> readable for as long as you keep the files. Because the code is MIT-licensed
> and open, there's no company in the middle that can raise a price, change the
> terms, or shut the doors.

> **UX direction.** Two- or three-column "deployment shapes" row: *On your
> machine* (desktop) / *Self-host a node* (office) / *Hosted node* (we run it).
> Show the same screenshot under each to make the point that it's one app. **Per
> Honesty ledger #4, only show the "Hosted node" column if CIC ratifies a hosted
> offering for launch.** Default to two columns (desktop + self-host) otherwise.

---

## Section 5 — Built from blocks (the architecture story, kept plain)

**Heading:** Turn on what you need

**Body:**
> Shipyard is assembled from building blocks — leases, rent collection,
> maintenance, inspections, accounting, tax — composed into workspaces.
> Property management today. Project management rolling in. More workspaces —
> facility operations, asset management — built on the same blocks.
> Start with what you need; the rest follows without switching tools.

> **Honesty note.** Only cockpit-complete verticals ship in v1. Project
> Management substrate is done — cockpit is in active build (lead v1 candidate).
> Facility Operations is secondary ("rolling in"); never "shipping." Asset
> Management and Acquisition/Underwriting are roadmap — do not imply they are
> near. Never name a count ("5 workspaces"); name the verticals that are
> confirmed shipped at the time of publication.

> **FED-facing UX direction.** Build Section 5 as a data-driven list — a
> `bundles[]` array in a config file, each entry carrying a
> `status: "live" | "rolling-in" | "roadmap"` field. The component reads the
> array and renders a status badge or ordering tier accordingly. That way a
> cockpit's status flips with one config change at launch (or post-launch slip)
> without touching the component or the copy. Example shape:
>
> ```ts
> const bundles = [
>   { name: "Property Management",       status: "live"       },
>   { name: "Project Management",        status: "rolling-in" },
>   { name: "Facility Operations",       status: "rolling-in" },
>   { name: "Asset Management",          status: "roadmap"    },
>   { name: "Acquisition/Underwriting",  status: "roadmap"    },
> ];
> ```
>
> "rolling-in" entries can be conditionally promoted to "live" or demoted to
> "roadmap" in the same config without a code change.

---

## Section 6 — Social proof (placeholder)

> **UX direction.** Reserve the band; do not fabricate testimonials, logos, or
> install counts — Shipyard is pre-launch OSS with no users yet (per memory:
> "user research scaffolding when products have users"). Acceptable launch-day
> fillers, in order of preference:
> - A GitHub star/CTA strip ("⭐ Star it on GitHub" + repo link) once public.
> - A single honest line: *"Shipyard is brand-new and open source. Kick the
>   tires, file an issue, tell us what your buildings actually need."*
> - A short "Built in the open" note linking the architecture paper / book.
>
> Swap to real quotes only once there are real operators using it. **Never
> placeholder-name a fake landlord.**

---

## Section 7 — Final CTA

**Heading:** Get your rentals out of the spreadsheet.

**Body:**
> Shipyard is free, open source, and yours to run. Bring your leases, rent, and
> books into one place — on your machine, on your terms.

**Primary CTA:** `Get started on GitHub` (or `Create your workspace` per the
hero CTA decision — keep both CTAs consistent)
**Secondary CTA:** `Read the docs`

---

## Section 8 — Footer essentials (copy snippets)

- **Tagline under logo:** *Open-source property management. MIT.*
- **One-liner:** *Shipyard is part of Harborline-Software — open-source tools you
  run yourself.*
- **Links:** GitHub · Docs · License (MIT) · The architecture behind it (book /
  paper) · Other Harborline projects.
- **No "© Harborline SaaS" / no "Terms of Service" implying a hosted account**
  unless a hosted offering ships and CIC ratifies it.

---

## Cross-references for the cross-fleet footer (consistency)

If the site carries a "more from Harborline" strip, use these one-liners
(consistent with fleet naming; keep adjacent products honestly scoped):

- **Sunfish** — the open-source UI framework Shipyard is built on. *(For
  developers.)*
- **Signal-bridge** — a comms mesh for connecting nodes. *(Adjacent; scope per
  ONR.)*
- **Flight Deck** — a media / story studio. *(Adjacent; different audience.)*
- **Tender** — a developer toolbox. *(Adjacent.)*

> Keep this strip minimal at launch; the lead product is Shipyard. Do not let
> the strip dilute the property-manager focus of the page.
