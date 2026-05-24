# Status Pill Pattern

**Pattern type:** Surface (component-level)
**First instance:** cohort-1 MaintenancePage `STATUS_COLORS` work-order status; cohort-2 AccountingPage days-due pill; cohort-3 PR 1 `<StatusPill kind=...>` promotion
**Pattern doc:** none (universal convention; canonical via `<StatusPill>`)

## Canonical shape

Inline pill rendered next to or inside text:

```
Status: [ Completed ]     (green pill — work-order status)
Aging:  [ 90+ days ]      (red pill — aging bucket)
Type:   [ Asset ]         (blue pill — GL account type)
```

## Visual base (pill chrome)

`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium`

This is the canonical pill base used across ALL variants. Applied via `<StatusPill>` automatically.

## Variant-driven coloring

| `kind=` | Variants | Color logic |
|---|---|---|
| `workOrderStatus` | Draft / Sent / Accepted / Scheduled / InProgress / Completed / OnHold / Cancelled | Per cohort-1 `STATUS_COLORS` map |
| `agingBucket` | NoBalance / Current / Days0To30 / Days31To60 / Days61To90 / Days90Plus | Per cohort-2 days-due-pill → cohort-3 `aging-bucket-pill` canonical |
| `glAccountType` | Asset (blue) / Liability (purple) / Equity (slate) / Revenue (green) / Expense (amber) | Per cohort-3 INDEX Q6 |
| `occupancyStatus` | Occupied (green) / NoticeGiven (amber) / Vacant (gray) / OffMarket (outlined gray) | Per cohort-3 INDEX Q7 |
| `balanceState` | Balanced (green) / OutOfBalance (red) | Per cohort-3 TrialBalancePage Q-B |

Full canonical color compositions live in `cohort-3/tokens.md`.

## Outlined variant suffix

When the semantic is "intentional absence" (e.g., `OccupancyStatus.OffMarket`), add `border border-gray-300` to the base. This produces a hollow pill — distinguishable from a filled pill at a glance:

```
[ Vacant ]    (gray filled)
[ OffMarket ] (gray outlined; lighter visual weight)
```

## Composition examples

### Inside a table cell

```
+-----------------------------------------------------------+
| TENANT             │ STATUS              │ DELINQUENCY    |
| ─────────────────  │ ──────────────────  │ ──────────     |
| Maria Santos       │ [ Occupied ]        │ [ Current ]    |
| James Harlow       │ [ NoticeGiven ]*    │ [ 31-60 d ]    |
| —                  │ [ Vacant ]          │ —              |
| —                  │ [ OffMarket ]**     │ —              |
+-----------------------------------------------------------+
   *amber + tooltip showing VacancyReason
   **outlined variant — intentional absence
```

### Adjacent to text inline

```
This account is [ Asset ] type and carries a balance of $12,500.
```

### In a list summary

```
1. Maria Santos       90+: $1,200     Total: $2,450
2. James Harlow       90+: $800       Total: $800
3. Anna Kowalski      90+: $—         Total: $300   [ Current ]
```

## Tooltip variant

When the pill needs to convey additional context (e.g., `NoticeGiven` with `VacancyReason`):

```
<StatusPill kind="occupancyStatus" value="NoticeGiven" tooltip="End of lease term — tenant has filed notice; vacate date 2026-07-15" />
```

Renders as:

```
[ NoticeGiven ]  ← hover shows tooltip
```

The tooltip is shown via native `title=` attribute (no JS tooltip library); standard accessibility behavior.

## When to use

- Status, type, or category indicators that benefit from color-coding
- Anywhere a small inline chip conveys a discrete state
- In tables (status / type columns)
- In dashboards (state summaries)

## When NOT to use

- Long-form text (use prose, not a pill)
- Free-form labels (use `<Badge>` primitive instead — generic chip without semantic-color binding)
- Action triggers (use a button, not a pill)

## Accessibility

- Pill text IS the primary signal; color is supplemental
- For `tooltip` variant, the tooltip content must be accessible to screen readers (via `title=` attribute, which is read by most SRs)
- Outlined variant: distinguishable from filled by border alone; still passes color-not-as-sole-signal test because the text content is unchanged

## Cross-references

- Component: `<StatusPill>` in `@sunfish/ui-react` (promoted cohort-3 PR 1)
- Token: pill base + per-kind canonical compositions in `cohort-3/tokens.md`
- Composes with: table-tfoot (inside cells); empty-state (sometimes shows a "Current" pill to signal "checked + zero")
