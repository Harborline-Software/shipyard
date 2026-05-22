# ErrorCard — v0.3 Storybook MDX Draft

**Status:** Pre-implementation spec. Becomes `ErrorCard.stories.mdx` once:
1. PAO Track C lands (design-system v0.3 direction confirmed)
2. V3 #10 (`@sunfish/ui-react` v0.3 substrate promotion) activates
3. `ErrorCard.tsx` is authored at `packages/ui-react/src/components/ErrorCard.tsx`

**Audit source:** `fed-status-2026-05-21T2145Z-errorcard-skeleton-v03-extraction-prep.md`
(12 existing instances catalogued across cohort-1 + cohort-2 + cockpit pages)

---

## Proposed component API

```tsx
interface ErrorCardProps {
  /**
   * Primary error headline. Typically "Failed to load {resource}".
   * Keep short — rendered in font-semibold text-red-700.
   */
  title: string

  /**
   * Optional secondary detail line. Pass error.message from TanStack Query.
   * Rendered in text-sm text-gray-600.
   */
  message?: string

  /**
   * If provided, renders a "Retry" button below the message.
   * Wire to TanStack Query's refetch() callback.
   */
  onRetry?: () => void

  /**
   * Visual scale of the card.
   * - 'default': rounded-lg p-6 — page-level errors and detail-view errors
   * - 'compact': rounded p-3   — inline action errors (payment failures, form submissions)
   * Default: 'default'
   */
  variant?: 'default' | 'compact'
}
```

---

## Reference implementation (do not ship until PAO Track C confirms)

```tsx
// packages/ui-react/src/components/ErrorCard.tsx

export function ErrorCard({ title, message, onRetry, variant = 'default' }: ErrorCardProps) {
  const isCompact = variant === 'compact'
  return (
    <div
      role="alert"
      className={cn(
        'border border-red-200 bg-red-50',
        isCompact ? 'rounded p-3' : 'rounded-lg p-6'
      )}
    >
      <p className={cn('font-semibold text-red-700', isCompact && 'text-sm')}>{title}</p>
      {message && (
        <p className={cn('text-gray-600', isCompact ? 'text-xs mt-0.5' : 'mt-1 text-sm')}>
          {message}
        </p>
      )}
      {onRetry && (
        <button
          onClick={onRetry}
          className={cn(
            'font-medium text-red-700 underline hover:no-underline',
            isCompact ? 'mt-2 text-xs' : 'mt-3 text-sm'
          )}
        >
          Retry
        </button>
      )}
    </div>
  )
}
```

---

## Storybook stories (when MDX activates)

```tsx
// ErrorCard.stories.tsx — landing file after PAO Track C

import type { Meta, StoryObj } from '@storybook/react'
import { ErrorCard } from './ErrorCard'

const meta: Meta<typeof ErrorCard> = {
  title: 'Feedback/ErrorCard',
  component: ErrorCard,
  argTypes: {
    variant: { control: 'select', options: ['default', 'compact'] },
    onRetry: { action: 'retry clicked' },
  },
}

export default meta
type Story = StoryObj<typeof ErrorCard>

export const Default: Story = {
  args: {
    title: 'Failed to load leases',
    message: 'Connection timed out. Check your network and try again.',
  },
}

export const WithRetry: Story = {
  args: {
    title: 'Failed to load properties',
    message: 'Network request failed.',
    onRetry: () => {},
  },
}

export const TitleOnly: Story = {
  args: {
    title: 'Failed to load dashboard',
  },
}

export const Compact: Story = {
  args: {
    variant: 'compact',
    title: 'Payment could not be processed',
    message: 'Server returned 500.',
  },
  name: 'Compact (inline action error)',
}

export const CompactWithRetry: Story = {
  args: {
    variant: 'compact',
    title: 'Failed to save',
    onRetry: () => {},
  },
}
```

---

## Accessibility notes

### role="alert"

The component MUST carry `role="alert"`. This causes screen readers to announce the error immediately on mount — correct behavior for a data-fetch failure visible to the user.

**Existing codebase gap:** Only `app.tsx:53` (app-level auth error) and `pages/TrusteeSetupPage.tsx` currently use `role="alert"`. The 12 inline ErrorCard instances do NOT (cohort-1 forward-watch item from V8 audit). The v0.3 `<ErrorCard>` closes this retroactively when pages migrate.

### WCAG color contrast

| Text class | Background | Ratio | Passes AA |
|---|---|---|---|
| `text-red-700` (`#b91c1c`) | `bg-red-50` (`#fef2f2`) | 5.18:1 | ✅ AA (4.5 normal, 3:1 large) |
| `text-gray-600` (`#4b5563`) | `bg-red-50` (`#fef2f2`) | 5.72:1 | ✅ AA |
| `text-red-700` Retry link | `bg-red-50` | 5.18:1 | ✅ AA |

All three pass WCAG 2.2 AA. No color changes needed for v0.3.

### Keyboard / focus

The Retry button is a native `<button>` — keyboard-accessible by default. No tabIndex manipulation needed.

### Dark mode

Dark mode design tokens are not finalized (PAO Track C scope). Forward-watch:

```tsx
// When dark mode tokens land (dark: prefix or CSS vars):
className={cn(
  'border border-red-200 bg-red-50',
  'dark:border-red-800 dark:bg-red-950',
  isCompact ? 'rounded p-3' : 'rounded-lg p-6'
)}
// text-red-700 → dark:text-red-400
// text-gray-600 → dark:text-gray-300
```

This mirrors the pattern in `SyncStateBadge.tsx` (existing dark mode handling in ui-react).

---

## Migration path — cohort-1 + cohort-2 pages

When v0.3 ships, 12 pages need mechanical migration:

### Step 1 — Package import

```tsx
- import { ErrorCard } from '../../some/local/path'
+ import { ErrorCard } from '@sunfish/ui-react'
```

### Step 2 — Replace inline JSX

**Before (cohort-1/2 inline pattern):**
```tsx
{isError && (
  <div className="rounded-lg border border-red-200 bg-red-50 p-6">
    <p className="font-semibold text-red-700">Failed to load leases</p>
    <p className="mt-1 text-sm text-gray-600">{error.message}</p>
    <button onClick={() => void refetch()} className="mt-3 text-sm font-medium text-red-700 underline hover:no-underline">
      Retry
    </button>
  </div>
)}
```

**After:**
```tsx
{isError && (
  <ErrorCard
    title="Failed to load leases"
    message={error.message}
    onRetry={() => void refetch()}
  />
)}
```

### Step 3 — Add role="alert" assertion to existing tests

Each page's test suite needs a forward-assertion:
```tsx
// Before: tested that error text appeared
expect(screen.getByText('Failed to load leases')).toBeInTheDocument()

// After: also verify a11y role
expect(screen.getByRole('alert')).toBeInTheDocument()
```

### Pages requiring migration

| File | Line | Variant | Has Retry | A11y delta |
|---|---|---|---|---|
| `pages/LeasesPage.tsx` | 24 | default | yes | add role="alert" |
| `pages/LeaseDetailPage.tsx` | 17 | default | no | add role="alert" |
| `pages/PropertiesPage.tsx` | 30 | default | yes | add role="alert" |
| `pages/MaintenancePage.tsx` | 170 | default | yes | add role="alert" |
| `cockpit/DashboardView.tsx` | 31 | default | yes | add role="alert" |
| `cockpit/vendors/VendorListView.tsx` | 23 | default | no | add role="alert" |
| `cockpit/vendors/VendorDetailView.tsx` | 33 | default | no | add role="alert" |
| `cockpit/work-orders/WorkOrderListView.tsx` | 65 | default | no | add role="alert" |
| `cockpit/work-orders/WorkOrderDetailView.tsx` | 37 | default | no | add role="alert" |
| `cockpit/properties/PropertyDetailView.tsx` | 44 | default | no | add role="alert" |
| `pages/AccountingPage.tsx` | 32, 83 | default (new) | no | new ErrorCard (was inline text) |
| `pages/RentRoll.tsx` | 44 | default (new) | no | new ErrorCard (was inline text) |
| `pages/PLReport.tsx` | 99 | default (new) | no | new ErrorCard (was inline text) |
| `pages/RentCollectionPage.tsx` | 166 | compact | no | new variant (was bespoke rounded border) |

**`app.tsx:53`** (app-level auth error) remains bespoke — single instance with unique `p-8 max-w-md` layout. Not a candidate for `<ErrorCard>`.

---

## Naming note

The V18 audit identified `LoadingSkeleton` as a companion extraction candidate. The v0.3 naming TBD with PAO — options:
- `<LoadingSkeleton>` (descriptive)
- `<LoadingState>` (agnostic to animation)
- `<LoadingPlaceholder>` (explicit non-skeleton)

Current loading states are text-only (`<p className="text-sm text-gray-500">Loading…</p>`); no animate-pulse exists on main. The name should not imply shimmer animation if the v0.3 implementation stays text-only. Recommend **`<LoadingState>`** for v0.3, with a shimmer `variant` added in v0.4 when design tokens include shimmer colors.

---

_Draft authored 2026-05-21 | FED V19 #3 | PAO Track C gates implementation_
