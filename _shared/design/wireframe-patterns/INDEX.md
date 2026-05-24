# ASCII Wireframe Pattern Library

**Status:** Authored 2026-05-22 (PAO; v2-batch #8).
**Scope:** Reusable ASCII wireframe patterns consolidated from cohort-2 + cohort-3 page docs.
**Audience:** PAO authoring new Track C; FED visualizing structural intent; Yeoman when reactivated.

---

## Use this library when

- Authoring a new per-page direction doc (cohort-4+) and you need a wireframe vocabulary
- Reviewing a FED PR and you want to compare the implementation against the canonical visual shape
- Designing a new component and you want to see how it composes with existing ones
- Onboarding a new contributor to "what our UI looks like in ASCII"

## Do NOT use this library when

- You need pixel-perfect visual mockups — use Figma / Yeoman wireframe pass
- You need interactive prototypes — use the live storybook
- You're authoring code — the wireframes are intent, not output

---

## Pattern catalog

### Structural patterns (page-level)

| Pattern | File | Use case |
|---|---|---|
| Report filter bar | [`filter-bar.md`](./filter-bar.md) | Any page with run-on-demand + parameters + Export CSV |
| Property/section block | [`property-block.md`](./property-block.md) | Any list grouped by parent entity (properties, accounts, customers) |
| Accordion list | [`accordion-list.md`](./accordion-list.md) | Hierarchical disclosure (collapsed parent → expanded children) |

### Surface patterns (component-level)

| Pattern | File | Use case |
|---|---|---|
| Table + tfoot | [`table-tfoot.md`](./table-tfoot.md) | Standard data table with totals row |
| Tile grid | [`tile-grid.md`](./tile-grid.md) | Portfolio/summary tile patterns |
| Provisionality banner | [`provisionality-banner.md`](./provisionality-banner.md) | Amber banner + collapsible details |
| Error surface | [`error-surface.md`](./error-surface.md) | Red surface + Retry button |
| Confirmation surface | [`confirmation-surface.md`](./confirmation-surface.md) | Green surface + next-action affordance |
| Status pill | [`status-pill.md`](./status-pill.md) | Inline status indicators |
| Empty state | [`empty-state.md`](./empty-state.md) | "No data" surface |

### Composition patterns (multi-component)

| Pattern | File | Use case |
|---|---|---|
| Run-on-demand report page | [`report-page-composition.md`](./report-page-composition.md) | Full cohort-3 report page composition (filter bar + provisionality + table + Export CSV) |

---

## Cross-references

- The canonical pattern documents (with full UX rationale + state machines) live in `cohort-3/` (`provisionality-banner-pattern.md` / `run-on-demand-pattern.md` / `csv-export-pattern.md`)
- The component inventory (which components implement these patterns) lives in `cohort-3/component-reuse-audit.md`
- The design tokens (which compositions back the visuals) live in `cohort-3/tokens.md`
- This library is the **visual shape** layer; for **interaction rules** see the pattern docs; for **implementation** see component-reuse-audit.

---

## Authoring conventions

When adding new wireframes to this library:

1. **Abstract, don't copy.** Each pattern doc captures the canonical shape, not a specific page's instance. Cite the page where it was first used; abstract the shape so it's reusable.

2. **Use the cohort-3 ASCII charset.** Box-drawing with `+`/`-`/`|` (not Unicode box-drawing — better grep + diff compatibility).

3. **Keep wireframes under 80 columns.** Wider wireframes don't render in narrow code-review surfaces.

4. **Annotate when needed.** A short prose paragraph before/after the wireframe naming the load-bearing elements.

5. **Cite the design tokens.** When a wireframe element corresponds to a canonical token (e.g., `provisional-surface`), name it.

6. **Update the cohort tokens.md if you canonicalize a new visual.** Library entries should point back to the cohort-N tokens.md for the authoritative naming.

---

## When to add a new pattern

A wireframe pattern earns a place in this library when:

- It's been used in ≥2 cohort page docs (not yet, but on this side of cohort-3 → cohort-4)
- OR it's a foundational shape (filter bar, table, error surface) that any cohort would likely reuse
- OR it's a particularly canonical instance the design language wants to preserve as exemplar (the cohort-3 report-page-composition is the latter)

A pattern does NOT earn a place when:

- It's specific to one page's data shape (e.g., `RentRollUnitRow`'s 7-column layout is specific; the table-tfoot pattern is reusable)
- It's a one-off design experiment that didn't get repeated
- It's already covered by another pattern with the same shape

---

— PAO, 2026-05-22
