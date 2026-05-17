---
type: brand-spec
status: draft-pending-cic-ratification
author: PAO
date: 2026-05-17
ratification-target: CIC
blocking: Yeoman Phase-4 marketing rebrand execution; book Vol 1+2 rename pass; pitch-narrative copy drafts
supersedes: implicit "Sunfish-the-platform / Anchor-the-app" model in vol-1 + vol-2 manuscripts
---

# Brand-Rebrand Spec — Sunfish-as-ERP + Book Vol 1+2 Rename

**Authority:** CIC direction 2026-05-17 T21:00Z — *"Sunfish is ERP product now, not platform. The inverted stack vol 1, 2 need to be rebranded to match the new names."*

**Status:** Draft. Yeoman does NOT execute until CIC ratifies the four flagged questions in `§ CIC questions for ratification` below. If CIC ratifies in part, partial execution proceeds against the ratified term-map subset.

---

## 1. What changed

Before today, the fleet brand stack was implicitly:

| Layer | Name (legacy) |
|---|---|
| Platform / reference architecture | **Sunfish** (the platform; the open-source reference impl) |
| Local-first desktop app | **Anchor** (the Sunfish accelerator) |
| Hosted-tenant SaaS shape | **Bridge** (the Sunfish accelerator) |
| Book's reference implementation | "Sunfish (`github.com/ctwoodwa/Sunfish`)" |

After CIC's 2026-05-17 direction, the fleet brand stack is:

| Layer | Name (new) | Source of truth |
|---|---|---|
| Platform / shared substrate | **Shipyard** | `shipyard/` repo (per RATIFICATION-2026-05-17 §1: "Platform — all shared .NET packages, TS packages, ADRs, ICM, tooling, design tokens") |
| Local-first property ERP (consumer-facing product) | **Sunfish** | `sunfish/` repo (per RATIFICATION-2026-05-17 §1: "Local-first property ERP (the Anchor app)") |
| Comms mesh | **Signal-Bridge** | `signal-bridge/` repo (already-renamed; not affected by this rebrand) |
| Media studio | **Flight-Deck** | `flight-deck/` repo (already-renamed; not affected) |
| Tray toolbox | **Tender** | `tender/` repo (already-renamed; not affected) |

The two surfaces this spec governs:
- **"Sunfish-the-platform"** → must become **"Shipyard"** wherever it referenced the shared substrate.
- **"Anchor"** → resolution depends on Q2 below. The RATIFICATION doc's prose "(the Anchor app)" treats Anchor as a synonym for the Sunfish product, which implies Anchor is being absorbed into Sunfish-the-product. CIC must confirm.

---

## 2. Term map (the operational table)

Yeoman uses this table once CIC ratifies. Every row marked `BLOCKED` waits on the CIC question of the same number.

| Legacy term | New term | Scope of rewrite | Status |
|---|---|---|---|
| "Sunfish" (the platform / reference impl) | **Shipyard** | Vol-1 preface, Vol-1 Ch11–16, Vol-1 appendix-g glossary, Vol-1 ch04 architecture diagrams, Vol-2 Ch07 ("Joel's Sunfish" title also affected — see Q4) | RATIFIED structurally; awaits Q1 confirm |
| "Sunfish" (the open-source GitHub project URL `github.com/ctwoodwa/Sunfish`) | **`github.com/Harborline-Software/shipyard`** (platform) + **`github.com/Harborline-Software/sunfish`** (ERP product) | Vol-1 preface §6 ("software described in this dissertation began on a laptop"); appendix-g; any in-text URL | RATIFIED structurally |
| "Sunfish.Kernel.Sync", "Sunfish.Foundation.LocalFirst" (package names) | **Defer.** C# namespace rename is Phase 3 (per RATIFICATION-2026-05-17 §"Phase 3+ deferred"). Until that phase ratifies, book text continues to reference `Sunfish.Kernel.*` etc. as-published — those are the *current* on-disk namespaces. | n/a for this rebrand pass | DEFERRED to Phase 3 |
| "Anchor" (the Zone A accelerator brand) | **BLOCKED — see Q2.** Options: (a) retire entirely, replace with "Sunfish" (the product); (b) keep as the desktop-shell brand name inside Sunfish marketing ("Sunfish runs on Anchor"); (c) keep as architectural pattern name ("Zone A is realized by the Anchor pattern") but drop from product marketing. | Vol-1 appendix-g, Vol-1 Ch17 (building-first-node), Vol-1 ch04, preface, Vol-2 SPINE; if (a), ~50 inline mentions across vol-1 + vol-2 | BLOCKED |
| "Bridge" (the Zone C accelerator brand) | **BLOCKED — see Q3.** The fleet now has a repo named `signal-bridge/` for comms mesh. "Bridge" as the SaaS-shape accelerator brand collides with that. Options: (a) rename to "Harbor" or "Pier" or another nautical noun; (b) keep "Bridge" inside book/architecture context and accept ambiguity with signal-bridge product; (c) retire "Bridge" entirely and refer to the Zone C pattern only by "Zone C". | Vol-1 appendix-g, Vol-1 Ch18 (migrating-existing-saas), Vol-1 ch04, preface; ~25 inline mentions | BLOCKED |
| Vol-2 Ch07 chapter title "Joel's Sunfish" | **BLOCKED — see Q4.** If "Sunfish-the-platform" is now Shipyard, the chapter title is now historically anchored to a prior name. Options: (a) leave title as-is (Vol-2 is a 2027-set period piece; the prior naming is canon-correct for that universe); (b) rename to "Joel's Shipyard" (matches new brand); (c) rename to "Joel's Local-First Stack" (decouples from product names entirely). | Vol-2 Ch07 title + ~40 in-prose mentions of "Sunfish" in Ch07 | BLOCKED |
| Internal repo names (`sunfish/`, `shipyard/`) | **No change.** Repo names follow CIC ratification of 2026-05-17. Decoupled from book prose. | n/a | RATIFIED |
| "Sunfish" (the ERP product, consumer-facing) | Confirmed as-is. **No rewrite.** This is the new canonical use of the word "Sunfish". | Marketing copy (pitch deck, OG, README hero, sales narrative) | RATIFIED |

---

## 3. Affected surfaces

### 3.1 Book Vol 1

| Surface | File path | Rebrand depth |
|---|---|---|
| Preface | `vol-1/front-matter/preface.md` | High — explicit "Sunfish" / "Anchor" / "Bridge" definitional paragraphs (lines 29, 47, 49) |
| Ch04 — Choosing Your Architecture | `vol-1/part-1-thesis-and-pain/ch04-choosing-your-architecture.md` | Medium — uses "Anchor" + "Bridge" as the named accelerators of Zone A / Zone C |
| Ch11 — Node Architecture | `vol-1/part-3-reference-architecture/ch11-node-architecture.md` | High — "Sunfish reference implementation" framing throughout |
| Ch12 — CRDT Engine and Data Layer | `vol-1/part-3-reference-architecture/ch12-crdt-engine-data-layer.md` | Medium — "Sunfish ships YDotNet" framing |
| Ch13–Ch16 — reference architecture | `vol-1/part-3-reference-architecture/ch13*.md`, `ch14*.md`, `ch15*.md`, `ch16*.md` | Medium — "Sunfish" as the implementing software |
| Ch17 — Building Your First Node | `vol-1/part-4-implementation-playbooks/ch17-building-first-node.md` | High — "Anchor accelerator" is the tutorial subject |
| Ch18 — Migrating an Existing SaaS | `vol-1/part-4-implementation-playbooks/ch18-migrating-existing-saas.md` | High — "Bridge accelerator" is the tutorial subject |
| Appendix A — Sync Daemon Wire Protocol | `vol-1/appendices/appendix-a-sync-daemon-wire-protocol.md` | Low — incidental |
| Appendix B — Threat Model Worksheets | `vol-1/appendices/appendix-b-threat-model-worksheets.md` | Low — incidental |
| Appendix C — Further Reading | `vol-1/appendices/appendix-c-further-reading.md` | Low — incidental |
| Appendix G — Glossary | `vol-1/appendices/appendix-g-glossary.md` | **Maximum** — definitional entries for Sunfish, Anchor, Bridge, Zone A, Zone B, Zone C |
| Epilogue | `vol-1/epilogue/epilogue-what-the-stack-owes-you.md` | Medium |

### 3.2 Book Vol 2

Vol-2 is a 2027-set first-person mission narrative ("Anna Yusupova's *Nansen* mission") in which "Sunfish" is referenced as in-universe technology. Q4 governs whether 2027-set narrative prose is rebranded or treated as period-accurate to the prior naming.

| Surface | File path | Rebrand depth |
|---|---|---|
| Vol-2 SPINE | `vol-2/SPINE.md` | Pending Q4 |
| Vol-2 CHAPTER-OUTLINE | `vol-2/CHAPTER-OUTLINE.md` | Pending Q4 |
| Ch07 — "Joel's Sunfish" | `vol-2/act-2/ch07-joels-sunfish.md` | **Maximum if Q4=(b); zero if Q4=(a)** |
| Other vol-2 chapters | Various | Low (incidental Sunfish mentions) |
| `_glossary/` | `vol-2/_glossary/relay.md`, `key-envelope.md`, etc. | Low |

### 3.3 Marketing surfaces (forward-going)

| Surface | Path | Notes |
|---|---|---|
| Pitch narrative scaffold | `sunfish/docs/marketing/pitch-narrative-scaffolding-2026-05-17.md` | Yeoman drafted today. PAO review still pending. Once this spec is ratified, pitch copy can proceed without ambiguity over which name lands where. |
| Future pitch deck slides | `shipyard/_shared/marketing/` (this folder, going forward) | Yeoman drafts under PAO review |
| README hero copy | `sunfish/README.md`, `shipyard/README.md` | Needs to land consistent with Q1 + Q2 ratification |
| OG images / brand assets | `shipyard/_shared/design/` (per fleet design-token home) | FED + Yeoman; depends on Q3 ("Bridge" naming) for whether a "Bridge brand token" still exists |
| Back-cover copy + sales narrative | `the-inverted-stack/docs/business-mvp/` (if it lands there) | PAO-authored after Q1–Q4 ratify |

---

## 4. CIC questions for ratification

The following four questions must ratify before Yeoman can execute the book rebrand pass. PAO's recommended answer is given in italics; CIC may override.

### Q1 — Does "Shipyard" land as the platform name across the book?

The RATIFICATION-2026-05-17 doc names `shipyard/` as the "Platform" repo. In-text book prose currently says "the Sunfish platform" / "the Sunfish reference implementation". Does this become "the Shipyard platform" / "the Shipyard reference implementation" in book prose?

*PAO recommends: yes. The repo name and the brand name should match. Reasoning: readers who go from book to GitHub need the name to be the same; the cost of book divergence outweighs the cost of one rebrand pass.*

### Q2 — Is "Anchor" retired or kept?

The RATIFICATION doc treats Sunfish-the-ERP as synonymous with "the Anchor app" — which suggests Anchor is being absorbed. But Anchor has been the brand for the Zone A *pattern* (not just the Sunfish product). Three options on the table:

- (a) **Retire entirely.** Replace "Anchor" with "Sunfish" (product) or "Zone A" (pattern) throughout. Cleanest.
- (b) **Keep as the desktop-shell name inside Sunfish marketing.** "Sunfish runs on the Anchor shell." Adds vocabulary.
- (c) **Keep as architectural pattern name only.** "Zone A is realized by the Anchor pattern." Decouples product from pattern.

*PAO recommends: (c). "Anchor" stays in book prose as the named realization of the Zone A pattern (architectural vocabulary); Sunfish-the-product marketing drops "Anchor" from its consumer surface. This preserves the book's architectural pedagogy while letting Sunfish-the-product brand cleanly.*

### Q3 — Is "Bridge" retired, renamed, or accepted to collide with `signal-bridge/`?

The Zone C accelerator brand "Bridge" now collides with the `signal-bridge/` repo. Three options:

- (a) **Rename Bridge to a non-colliding noun.** "Pier", "Harbor", "Quay" all work nautically. Adds a rebrand surface but preserves architectural vocabulary.
- (b) **Accept the collision.** Book uses "Bridge" for Zone C accelerator; product line uses "Signal-Bridge" for comms mesh. Context-dependent disambiguation. Costs nothing in prose; costs reader clarity.
- (c) **Retire entirely.** Drop "Bridge" as an accelerator brand; refer to Zone C only by "Zone C" or "the Zone C accelerator". Cleanest.

*PAO recommends: (c). Same reasoning as Q2 — Zone C is the architectural concept and that vocabulary stands on its own. Retiring "Bridge" removes the collision and reduces vocabulary load on the reader.*

### Q4 — Is Vol-2 Ch07 retitled? Is Vol-2 prose rebranded?

Vol-2 is a 2027-set mission narrative. In-universe, the Sunfish-of-2027 was named what it was named at the time the events occur. Three options:

- (a) **Leave Vol-2 as-is.** "Joel's Sunfish" stays. Vol-2 is period-accurate to the prior naming era. Reader infers from preface that "Sunfish" in vol-2 prose is the historical name.
- (b) **Retitle Ch07 to "Joel's Shipyard" + rebrand vol-2 prose.** Aligns vol-2 to new brand. Costs ~40 in-prose mentions across Ch07 + lower mentions across other chapters.
- (c) **Retitle Ch07 to "Joel's Local-First Stack" + decouple vol-2 prose from product names.** Removes vol-2 from the rebrand surface entirely.

*PAO recommends: (a). Vol-2 is a period piece. The current naming is canon-correct for the 2027 setting and a forced rebrand would damage the in-universe coherence. A Vol-1 preface footnote can clarify: "Throughout Volume 2, the platform is referred to by its 2027-era name, Sunfish. The current name is Shipyard." This is the cheapest option and the one that best respects the narrative.*

---

## 5. Execution plan (after CIC ratification)

Once CIC answers Q1–Q4, Yeoman executes in this order:

1. **Vol-1 Appendix G** (glossary) — rewrite entries for Sunfish, Anchor, Bridge, Zone A/B/C; the glossary is the authority for terminology elsewhere in the book.
2. **Vol-1 Preface** — rewrite the three Sunfish/Anchor/Bridge definitional paragraphs.
3. **Vol-1 Ch04 + Ch11–Ch18** — search-and-replace pass governed by the ratified term map; PAO reviews per-chapter.
4. **Vol-2 prose** — only if Q4 ≠ (a); otherwise vol-2 gets only a preface footnote clarifying era-of-name.
5. **Marketing forward-copy** — pitch narrative, README, OG, back-cover; all written against the ratified term map from day one.
6. **Book back-matter pass** — confirm citations, URLs, and "where to find the code" copy land at `Harborline-Software/shipyard` and `Harborline-Software/sunfish`.

Yeoman files a status beacon at each step and PAO reviews before next step proceeds.

---

## 6. Out of scope

- C# namespace rename (`Sunfish.*` → `Shipyard.*`) is **Phase 3 of the fleet migration** (per RATIFICATION-2026-05-17 §"Phase 3+ deferred"). Book code references continue to use `Sunfish.Kernel.*` etc. until Phase 3 ratifies and lands.
- Source paper rebrand (`local_node_saas_v13.md`, `inverted-stack-v5.md`) — these are gitignored historical artifacts; not rewritten.
- Historical ICM hand-offs and ADRs — frozen historical content per RATIFICATION §"Outstanding watch-items §3".
- `flight-deck/`, `signal-bridge/`, `tender/` brand names — already ratified, not touched by this spec.

---

## 7. Confidence + risk

**PAO confidence:** Medium-high on Q1 (Shipyard lands cleanly). Medium on Q2 + Q3 (architectural vocabulary debate). High on Q4 (vol-2 as period piece is the clear right answer; the cost-benefit is one-sided).

**Risk if not ratified soon:** Yeoman's pitch-narrative draft (already filed) and any forward marketing copy ship with ambiguity that becomes expensive to retrofit. The longer the ambiguity sits, the more downstream copy locks in a guess. Recommend CIC ratification within 7 days.

— PAO
