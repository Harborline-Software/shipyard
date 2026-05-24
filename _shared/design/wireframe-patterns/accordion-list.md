# Accordion List Pattern

**Pattern type:** Structural (page-level)
**First instance:** cohort-3 ProfitAndLossByPropertyPage `<PropertyAccordionList>`
**Pattern doc:** `cohort-3/03-profit-loss-by-property-page.md` (Q-A: chevron + expanded blue-bottom-border)

## Canonical shape

### Collapsed (default for multi-property results)

```
+--------------------------------------------------------+
| ▶  150 Lexington Ct       $24,500  $8,200    $16,300   |
+--------------------------------------------------------+
| ▶  240 Park Ave South     $18,000  $4,100    $13,900   |
+--------------------------------------------------------+
| ▶  Unassigned (italic)        $500    $200       $300  |
+--------------------------------------------------------+
```

### Expanded (one property opened)

```
+--------------------------------------------------------+
| ▼  150 Lexington Ct       $24,500  $8,200    $16,300   |
+--------------------------------------------------------+
|     Revenue                                            |
|        4100  Rent income           $22,000              |
|        4150  Late fees                $500              |
|        4200  CAM reimbursements     $2,000              |
|                                                         |
|     Expenses                                            |
|        5100  Utilities              $3,200              |
|        5200  Maintenance            $4,500              |
|        5300  Insurance                $500              |
+--------------------------------------------------------+
| ▶  240 Park Ave South     $18,000  $4,100    $13,900   |
+--------------------------------------------------------+
| ▶  Unassigned (italic)        $500    $200       $300  |
+--------------------------------------------------------+
```

## Load-bearing elements

- **Chevron**: `▶` collapsed; `▼` expanded (uses Heroicon `ChevronRightIcon` / `ChevronDownIcon`)
- **Expanded header**: `bg-gray-50` + `border-b-2 border-blue-500` (the blue bottom-border anchors the expansion visually)
- **Hover on collapsed header**: `hover:bg-gray-50 cursor-pointer`
- **Expanded body**: `bg-white px-4 py-3 border-b border-gray-200`; indented with `pl-6` to signal hierarchy
- **Section accents inside expanded body**: Revenue with green-left-border; Expenses with red-left-border (`border-l-2 border-green-500 pl-2` / `border-l-2 border-red-500 pl-2`)
- **"Unassigned" row**: italic + de-emphasized text (`text-gray-500 italic`); always sorts LAST regardless of alphabetical position

## Auto-expand behavior

If the result has exactly one parent (e.g., one property), auto-expand it on initial render. No special UI; the accordion just opens by default. Multi-parent results stay all-collapsed on first render.

## State variants

### Single property (auto-expanded)

Same shape as "expanded" above but no other accordion rows; one block.

### All collapsed (multi-property)

Default initial state for multi-parent results. User clicks to expand individual properties.

## When to use

- Hierarchical data where the parent's summary matters at-a-glance but the children's detail is opt-in
- When users typically want to scan parents first + drill into one or two for detail
- When the total number of children would overwhelm the visible region if all expanded

## When NOT to use

- Flat data with no hierarchy (use table-tfoot)
- Data where children matter immediately (use property-block — always-visible rows)
- Single-parent results where there's nothing to choose between (auto-expand the one accordion = same as just showing the data)

## Accessibility

- Accordion uses `<button aria-expanded={open} aria-controls={bodyId}>` for the header row
- Body: `<div id={bodyId} role="region" aria-label="..." hidden={!open}>`
- Keyboard: Tab to focus header, Space/Enter to toggle
- Revenue/Expense section accents are NOT the sole signal — section heading text + amount sign convey meaning

## Cross-references

- Pattern doc: `cohort-3/03-profit-loss-by-property-page.md`
- Component: page-local (`PropertyAccordion` + `PropertyAccordionList`) in cohort-3 P&L page; NOT promoted to shared (pending 2nd accordion consumer)
- Token: expanded header → `bg-gray-50 border-b-2 border-blue-500`
- Composes with: tile-grid pattern (for the portfolio summary tiles above the accordion list)
