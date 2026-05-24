# Property/Section Block Pattern

**Pattern type:** Structural (page-level)
**First instance:** cohort-3 RentRollPage `<PropertyBlock>` (per-property grouping with header + unit table)
**Pattern doc:** `cohort-3/04-rent-roll-page.md` (Q-A resolution: "divider-bar approach")

## Canonical shape

```
+--------------------------------------------------------+
| 150 Lexington Ct                                       |
| 12 units | 10 occupied (83%) | $15,200/mo | $340 open  |
+--------------------------------------------------------+
|  UNIT       │  TENANT             │ LEASE END │ ...    |
|  ─────────  │  ─────────────────  │ ────────  │        |
|  101        │  Maria Santos       │ 2027-04   │ ...    |
|  102        │  James Harlow       │ 2026-12   │ ...    |
|  103        │  —                  │ —         │ ...    |
+--------------------------------------------------------+

+--------------------------------------------------------+
| 240 Park Ave South                                     |
| 8 units | 8 occupied (100%) | $24,000/mo | $0 open     |
+--------------------------------------------------------+
|  UNIT       │  TENANT             │ LEASE END │ ...    |
|  ...        │  ...                │ ...       │ ...    |
+--------------------------------------------------------+
```

## Load-bearing elements

- **Divider-bar header** — `bg-gray-50 border-b border-gray-200 px-4 py-3` with property name (left, `text-base font-semibold text-gray-900`) + single-line summary stats (right, `text-sm text-gray-600`)
- **Inline table directly below** — no card chrome around the table; the divider bar IS the property's visual anchor
- **Vertical spacing between blocks** — implicit; each block is its own grid row in the parent list
- **Sort:** properties sorted by name ascending; "Unassigned" if present sorts last + de-emphasized (`text-gray-500 italic`)

## Why this shape (vs alternatives considered)

Per cohort-3 ch04-rent-roll-page.md Q-A:
- **NOT a full card header** (competes visually with the unit data)
- **NOT an icon** (adds noise without aiding scanning)
- **NOT a card border around the whole block** (treats property as a container; the divider treats property as a section heading, which matches the reading order: header → rows)

## State variants

### Single-property mode

If only one property in result, the entire page can still use the property-block pattern — just one block instead of N. No special collapsed/single-property mode.

### Empty property block (zero units)

```
+--------------------------------------------------------+
| 150 Lexington Ct                                       |
| 12 units total | 0 visible (filter excludes)            |
+--------------------------------------------------------+
| No units match the current filter.                     |
+--------------------------------------------------------+
```

When a property has zero units after filtering (e.g., `includeVacant === false` and all units are vacant), the block can be hidden entirely OR show a one-line "no units match" notice. Hide-entirely is the default per cohort-3 RentRoll.

## When to use

- Any page where data is naturally grouped by a parent entity (property, customer, vendor, account)
- When the parent's identity matters (so it gets visual presence) but doesn't need disclosure controls (use the accordion pattern instead if rows should collapse)

## When NOT to use

- Flat tables with no grouping (use plain table-tfoot pattern)
- Hierarchical data with collapse/expand (use accordion-list pattern)
- Tile dashboards (use tile-grid pattern)

## Cross-references

- Component: page-local (`PropertyBlock` + `PropertyHeader`) in cohort-3 RentRollPage; NOT promoted to shared (pending 2nd consumer)
- Token: divider-bar header → `bg-gray-50 border-b border-gray-200 px-4 py-3`
- Composes with: table-tfoot pattern (for the table inside the block)
