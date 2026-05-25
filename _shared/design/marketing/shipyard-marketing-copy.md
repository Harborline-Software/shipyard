# Shipyard — Marketing Copy

**Product:** Shipyard, open-source property-management platform
**License:** MIT
**Parent:** Harborline-Software
**Audience:** Small property-management firms and individual landlords (5–20 units)
**Register:** Practical, plain-spoken, operator-friendly. Concrete verbs over abstractions.
**Author:** PAO, 2026-05-25
**Status:** Draft — pending PAO review

> **Voice note for downstream use.** Write like you are talking to a landlord
> who manages a dozen units from a kitchen table and is sick of spreadsheets.
> Say what the software does, not how it makes them feel. "Track your leases.
> See who owes what. Close your books at month-end." Banned words: *powerful*,
> *seamless*, *all-in-one*. No claims that imply a Harborline-hosted service —
> Shipyard is self-hosted MIT software; the operator runs it and owns the data.

---

## 1. GitHub repository description (≤160 chars)

**Primary (147 chars):**

> Open-source property management for landlords and small firms. Track leases, rent, and the books — all on your own machine. Self-hosted, MIT.

**Alternate A (139 chars):**

> Self-hosted property management for landlords. Leases, rent roll, and accounting that live on your machine, not someone else's. MIT.

**Alternate B (122 chars):**

> Property management for small landlords. Track leases, see who owes what, close your books. Self-hosted and open source.

---

## 2. README hero (~200 words)

# Shipyard

Property management without the monthly bill.

If you manage a handful of rentals, you already know the drill: one
spreadsheet for leases, another for rent, a shoebox for receipts, and a
nagging sense at tax time that something doesn't add up. The software built
to fix that usually charges per unit, per month, forever — and keeps your
records on a server you'll never see.

Shipyard takes a different line. It's a property-management app you run
yourself. Track every lease, see who has paid and who hasn't, and close your
books at month-end without exporting anything to a vendor first.

Your data lives on your machine. Not on our servers — we don't have any.
When you want your records on a second computer, Shipyard syncs them. When
you don't, it stays put. Either way, the files are yours, and they stay
readable whether or not this project is still around in ten years.

Shipyard is free and open source under the MIT license. No seats to buy, no
plan to upgrade, no lock-in. Clone it, run it, and get your rentals out of
the spreadsheet.

Built for landlords with 5 to 20 units and the small firms that manage them.

---

## 3. Landing page copy (~400 words)

### Hero

**Headline:** Your rentals. Your books. Your machine.

**Subhead:** Shipyard is open-source property management that runs on your
own computer — track leases, rent, and accounting without a monthly bill or
a vendor holding your records.

**Primary CTA:** Get Shipyard on GitHub
**Secondary CTA:** See how it works

---

### Features

**Lease management**
Keep every lease in one place — tenants, terms, dates, and renewals. See at
a glance which leases are active, which are ending soon, and which need a
signature.

**Rent roll**
One screen that shows every unit, its rent, and its current tenant. Know
what you should be collecting this month before you go chasing it.

**See who owes what**
Accounts-receivable aging sorts unpaid balances by how late they are, so the
30-, 60-, and 90-day problems surface on their own. No more guessing who to
call first.

**Track what you owe**
Accounts-payable aging does the same for your bills — vendors, contractors,
and dues — so nothing slips past its due date and nothing gets paid twice.

**Close the books**
A trial balance and an accounting overview let you square the month without
exporting your data to anyone. The numbers are right there, and they're
yours to check.

**Property detail at a glance**
Open any property and see its leases, its tenants, and its balances on one
page — the answer to "how's that building doing?" without opening four tabs.

---

### Who it's for

Shipyard is built for landlords who manage 5 to 20 units and for the small
firms that look after a handful of buildings. If you've outgrown
spreadsheets but a per-unit SaaS subscription feels like paying rent on your
own records, this is for you. It runs on Mac and Windows, on the desktop or
in a browser, and it works whether or not you're online.

---

### Why open source and local-first

Your records sit on your machine. Shipyard never sends them anywhere you
don't tell it to, and there's no company in the middle that can raise a
price, change the terms, or shut the doors. Because it's MIT-licensed, the
code is open for anyone to read, run, and improve — and your data stays in a
format you can open for as long as you keep the files.

---

### CTA block

**Heading:** Get your rentals out of the spreadsheet.

**Body:** Shipyard is free, open source, and yours to run. Clone the repo,
start it on your own machine, and bring your leases, rent, and books into one
place.

**Primary CTA:** Get started on GitHub
**Secondary CTA:** Read the docs

---

## 4. npm / package short description (≤80 chars)

**Primary (72 chars):**

> Self-hosted, open-source property management for small landlords.

**Alternate A (78 chars):**

> Local-first property management — leases, rent, and books on your machine.

**Alternate B (64 chars):**

> Open-source property management you run yourself. MIT.

---

## Notes for reviewers and downstream agents

- **Brand name is "Shipyard."** The repo directory is still `sunfish/` and the
  internal design-system package is now named "Sunfish" — do not confuse the
  two. Public-facing copy never says "Sunfish" for this app.
- **No SaaS claims.** Every line is written so it stays true for self-hosted
  MIT software. There is no "our platform" or "our servers." If a future
  managed-hosting offering appears, that copy is a separate deliverable.
- **Feature list tracks shipped + in-flight cohorts.** AP aging (cohort-4) is
  in flight at time of writing; the landing-page feature is phrased so it
  holds the moment that page ships. Confirm AP aging is live before this copy
  goes public, or trim that feature card.
- **Numbers register.** "5 to 20 units" is the stated target band; keep it
  concrete rather than "any size."
