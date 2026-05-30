# Shipyard — Launch-Day Messaging

**Author:** PAO, 2026-05-29
**Status:** Draft — pending Admiral / CIC review
**Scope:** Short announcement copy for README banner, site banner, and social.
All variants are honest to shipped capability (property-management cockpit;
self-hostable; MIT) and avoid the unverified-signup and overreaching-offline
claims flagged in `positioning-one-pager.md` (Honesty ledger). Voice + banned
words inherited from `../shipyard-marketing-copy.md`.

---

## A. One-paragraph announcement (README banner / blog lede, ~90 words)

> **Shipyard is open-source property management for small landlords.** Track
> every lease, see who has paid and who hasn't, run your rent roll, and close
> your books at month-end — all in one place you control. It's MIT-licensed and
> yours to run: on your own machine, on a node you host, with your data
> exportable to plain JSON or CSV any time. No per-unit bill, no vendor holding
> your records. Shipyard is part of Harborline-Software — open-source tools you
> run yourself. Get it on GitHub.

---

## B. Tighter announcement (site top-banner / newsletter, ~45 words)

> Shipyard is now open source. It's property management for small landlords —
> leases, rent roll, who-owes-what, and the books, in one place you run
> yourself. MIT-licensed, your data stays yours. Part of Harborline-Software.
> Get it on GitHub.

---

## C. Social post — long form (Mastodon / LinkedIn, ~3 short paragraphs)

> If you manage a handful of rentals, you know the drill: one spreadsheet for
> leases, another for rent, a shoebox for receipts, and a bad feeling at tax
> time.
>
> We built Shipyard to fix that. It's property management you run yourself —
> track leases, see who owes what, run your rent roll, and close your books, all
> in one place. No per-unit subscription, no vendor holding your records. Your
> data is yours and exports to plain CSV any time.
>
> Shipyard is open source under the MIT license, part of Harborline-Software.
> Kick the tires and tell us what your buildings actually need. → [GitHub link]

---

## D. Social post — short form (X / Bluesky, ≤280 chars)

> Shipyard is open source. Property management for small landlords: leases, rent
> roll, who-owes-what, and the books — in one place you run yourself. No monthly
> bill, no vendor holding your records. MIT-licensed. → [GitHub link]
>
> *(218 chars without the link.)*

---

## E. One-line hooks (reusable everywhere)

- *Property management you run yourself.*
- *Your rentals. Your books. Your machine.*
- *Get your rentals out of the spreadsheet.*
- *Open-source property management. No monthly bill, no middleman.*
- *The property app that doesn't own your data.*

> **Platform-frame A/B variant (optional trailing clause).** If a second cockpit
> (Project Management) lands in v1, every hook in section E — and the paragraph
> announcements A and B — can carry this optional closing clause:
> *"— the first workspace on an open, multi-vertical platform."*
>
> If only the Property-Management cockpit ships, DROP the clause entirely. The
> PM-led hooks stand on their own; the multi-vertical frame is additive, not
> load-bearing. Never hard-code the clause into a version that goes to press
> before the cockpit-complete set is confirmed.

---

## F. Hacker News / dev-audience variant (different register, ~70 words)

> **Show: Shipyard — open-source, self-hostable property management (MIT)**
>
> Shipyard is a property-management app for small landlords — leases, rent roll,
> AR aging, accounting, maintenance — built to run on your own machine or a node
> you host, with data exportable to JSON/CSV. It's the reference implementation
> of a local-first / hosted-node architecture (kernel + composable blocks +
> business-case bundles). React + .NET, MIT-licensed. Feedback welcome,
> especially from anyone who actually manages buildings.

> **Honesty note for this variant.** The "kernel + composable blocks + bundles"
> claim is verifiable (kernel-* and blocks-* packages + bundle manifests exist).
> Do NOT claim "fully offline-capable" or "one-click hosted signup" here — the HN
> crowd will check, and neither is true today (see one-pager Honesty ledger
> #1, #3).

---

## Guardrails recap (apply to every variant before publishing)

1. **Names:** "Shipyard" = the property app. "Sunfish" = the UI framework. Never
   call the app Sunfish in public copy.
2. **No unverified signup promise.** Don't say "sign up in seconds" until the
   signup -> verify -> login flow is merged and the auth smoke-test passes.
3. **No full-offline claim for the web app.** "Run it on your machine /
   self-host" is true; "works offline in your browser" is not (yet).
4. **Self-hosted-first.** Lead with run-it-yourself + open source. There is no
   Harborline-hosted signup in v1; never imply one. Managed hosting is a roadmap
   line only, never a present "sign up." (CIC ruled Request 4, 2026-05-30.)
5. **No fabricated proof.** No fake users, testimonials, logos, or install
   counts at launch.
6. **Banned words:** *powerful*, *seamless*, *all-in-one*, *revolutionary*,
   *enterprise-grade*, *game-changing*.
7. **Don't name a count of products at launch.** Say "property management today,
   more rolling in" — not "5 workspaces" or "4 verticals." A cockpit can slip
   between final build and launch day; the copy must survive that slip unchanged.
   Name only the verticals confirmed shipped; let the others be implied by
   "more rolling in."
