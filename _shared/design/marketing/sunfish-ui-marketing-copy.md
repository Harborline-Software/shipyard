# Sunfish UI — Marketing Copy

**Product:** Sunfish — open-source React UI component library and design system (MIT)
**Package:** `@sunfish/ui-react`
**Repo:** `shipyard/` (Harborline-Software)
**Audience:** Solo developers and startup teams building data-dense, local-first
web and desktop apps
**Author:** PAO, 2026-05-25
**Source material:** `coordination/inbox/pao-status-2026-05-22T18-09Z-design-velocity-case-study.md`;
`_shared/design/design-system-overview.md`

**Register note for editors:** Developer-community tone throughout. Honest,
technical, no marketing fluff. The differentiator is the cohort-based design
contract model — lead with it. The 8x wall-clock improvement (cohort-2 to
cohort-3) is the proof point. Avoid "powerful," "seamless," "delightful." Do
not overstate the component count. Do not claim it fits every use case — it is
built for data-dense, enterprise-adjacent apps (property management,
ERP-adjacent, reporting-heavy pages).

---

## 1. GitHub repository description (160 chars max)

For the `shipyard/` repo About field.

> React component library for data-dense apps. Design contracts are pinned
> before build, so new pages don't re-litigate the design every time. MIT.

(157 characters.)

Alternate, if a shorter form is needed:

> Open-source React + Tailwind UI for data-dense apps. Pin the design contract,
> then build against it. MIT.

(102 characters.)

---

## 2. README hero (~200 words)

For the top of the shipyard GitHub README and the `@sunfish/ui-react` npm page.

---

# Sunfish

A React + TypeScript component library for data-dense applications — aging
reports, rent rolls, trial balances, work-order forms, and the rest of the
table-and-totals surface area that enterprise-adjacent apps are mostly made of.
Built on Tailwind CSS and Shadcn primitives. MIT-licensed.

Sunfish is not trying to be a general-purpose kit. It is built for the kind of
app where a screen is a filter bar, a long table, a few summary tiles, and a
correct currency column — and where getting those details consistent across
twenty pages is the actual work.

What makes it different is how the design gets decided. Most component libraries
hand you parts and leave you to negotiate layout, state, and copy on every new
page. Sunfish ships with a **design contract model**: each page's design
direction is pinned in a document before the component work starts. Patterns,
tokens, empty states, and error copy are settled up front, so building a new
page is implementation against a contract — not another round of design
arbitration.

This is a growing library with a strong, opinionated foundation, used in
production by the Shipyard property-management platform. Start with the
components; adopt the contract model when you have more than a handful of pages
to keep coherent.

```bash
npm install @sunfish/ui-react
```

---

## 3. Landing page copy (~500 words)

### Hero

**Headline:** Build the twentieth page as fast as the first.

**Subhead:** Sunfish is a React component library for data-dense apps that pins
the design contract before the build — so adding a page is implementation, not
another design negotiation.

### Problem statement

A component library gives you parts. It does not give you decisions. So every
new page reopens the same questions: which pattern applies here, what does the
empty state say, how does this error read, what color is this badge. On page
three it's a conversation. On page twenty it's a tax. The library was supposed
to make you faster, and instead each screen costs about what the last one did —
because the design work never compounds.

### How Sunfish works

Three steps, in order. The order is the point.

1. **Pin the contract.** Before any component is touched, the page's design
   direction is written down: the patterns it uses, the tokens it composes, the
   states it must handle, the exact copy for banners and errors. Open questions
   get answered here, once.
2. **Build in parallel.** With the contract pinned, multiple pages can be built
   at the same time without stepping on each other. Nobody is waiting on a
   design decision mid-build, because the decisions already exist. There is
   nothing to arbitrate.
3. **Ship.** The implementation matches the contract because the contract came
   first. The pinned document is the source of truth — for the build, for
   review, and for the next person who touches the page.

The measured result: one batch of four pages was specified in 75 minutes of
wall-clock time. The previous batch, before the contract model was in place,
took roughly ten hours for three pages. That is an 8x improvement across a
single iteration — with more pages shipped, not fewer. The gains narrow as the
system matures, but the direction holds: the marginal cost of a new page falls.

### Features

- **Data-dense components.** Tables, aging-bucket pills, currency cells,
  summary tiles, filter bars, and report surfaces. Built for the screens that
  are mostly numbers. Locale-aware formatting and tabular alignment are defaults,
  not afterthoughts.
- **Design contract model.** Each surface ships with a written design direction
  that pins patterns, tokens, and copy before the build. New pages implement
  against a contract instead of re-deciding the design.
- **A finite token vocabulary.** Sunfish has never added a custom Tailwind
  palette stop. Every color, surface, and pill variant composes the stops you
  already have, so the visual language stays small enough to hold in your head.
- **Accessibility as default.** WCAG AA contrast, semantic HTML over ARIA,
  color never as the sole signal, `role="alert"` on errors. The a11y baseline is
  built into the components, not bolted on per page.
- **Reference implementation included.** The Shipyard property-management
  platform is built on Sunfish in production. The components have shipped real
  reporting pages, not just a Storybook.

### Who it's for

Solo developers and small teams building local-first web or desktop apps with
real data density — property management, ERP-adjacent tooling, reporting-heavy
internal apps. If your app is mostly tables, totals, and forms, and you have
more pages coming than you've already built, Sunfish is built for your shape.

If you're building a marketing site or a consumer app with lots of bespoke
layout, reach for a general-purpose kit instead. Sunfish is opinionated on
purpose.

### CTA

**Install `@sunfish/ui-react` and build your first page. Read the contract guide
when you're ready for your fifth.**

```bash
npm install @sunfish/ui-react
```

---

## 4. Component gallery intro (~150 words)

Sits above the component list on the docs/showcase page.

---

This gallery is the working inventory of what Sunfish ships today — every
component that has earned its place by appearing across real pages, not a
wishlist. You'll find the data-dense primitives first: tables and table chrome,
currency cells, aging-bucket and status pills, summary tiles, filter bars, and
the report surfaces (provisionality banners, run buttons, CSV export). Each
entry shows its props, its variants, and the page patterns it's meant to
compose into.

A note on what's here and what isn't. Sunfish promotes a component to the shared
library only after it has shown up in more than one place — a single-use widget
stays local to its page until a second consumer proves the abstraction. So this
list is deliberately shorter than a general-purpose kit's, and every item on it
is load-bearing. If you're looking for a component that isn't here yet, check
the promotion backlog in the design-system overview; it may be a deferred
candidate rather than a gap.

---

## 5. Adoption guide opener (~100 words)

The first paragraph of a "porting to Sunfish" guide.

---

Porting to Sunfish is a good fit if your app is built from tables, totals, and
forms and you want them consistent without hand-maintaining the consistency.
Here's the honest shape of it: you get a coherent set of data-dense components,
a finite token vocabulary that composes the Tailwind palette you already have,
and a design contract model that makes the *next* page cheap. What you sign up
for is Tailwind CSS and Shadcn primitives as a baseline, a peer dependency on
React 18+, and a few opinions you'll need to adopt rather than configure around —
explicit run actions on reports, non-dismissible provisionality banners, audit
acknowledgment on write paths. If those opinions match how your app already
thinks, the port is mostly mechanical. If they don't, read the contract guide
before you start so you know what you're agreeing to.

---

— PAO, 2026-05-25
