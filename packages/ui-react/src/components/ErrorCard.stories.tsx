import type { Meta, StoryObj } from '@storybook/react'
import { ErrorCard } from './ErrorCard'

const meta: Meta<typeof ErrorCard> = {
  title: 'UI/ErrorCard',
  component: ErrorCard,
  tags: ['autodocs'],
  parameters: {
    docs: {
      description: {
        component:
          'Consistent error state card for data-fetch failures. Three variants: ' +
          '`page` (full-page error boundary, p-8, h2 heading), ' +
          '`default` (section-level error, p-6), ' +
          '`compact` (inline/sub-section error, p-4, smaller retry button). ' +
          'All variants accept an optional `onRetry` callback that renders a Retry button. ' +
          'Root element carries `role="alert"` for screen readers.',
      },
    },
    layout: 'padded',
  },
}

export default meta
type Story = StoryObj<typeof ErrorCard>

export const Default: Story = {
  args: {
    title: 'Failed to load leases',
    message: 'Network request failed: 503 Service Unavailable',
    onRetry: () => alert('retry'),
    variant: 'default',
  },
  parameters: {
    docs: {
      description: {
        story: 'Standard section-level error — used in page-level query failures (p-6).',
      },
    },
  },
}

export const Page: Story = {
  args: {
    title: 'Something went wrong',
    message: 'An unexpected error occurred. If this persists, please contact support.',
    onRetry: () => alert('retry'),
    variant: 'page',
  },
  parameters: {
    docs: {
      description: {
        story: 'Full-page error boundary fallback — h2 heading, p-8 padding.',
      },
    },
  },
}

export const Compact: Story = {
  args: {
    title: 'Failed to load work orders',
    message: 'Timeout after 10s',
    onRetry: () => alert('retry'),
    variant: 'compact',
  },
  parameters: {
    docs: {
      description: {
        story: 'Compact variant for inline/sub-section errors — p-4, smaller retry button.',
      },
    },
  },
}

export const NoRetry: Story = {
  args: {
    title: 'Failed to load vendors',
    message: '404 Not Found',
    variant: 'default',
  },
  parameters: {
    docs: {
      description: {
        story: 'When `onRetry` is omitted the Retry button is hidden — use when the error is unrecoverable.',
      },
    },
  },
}

export const TitleOnly: Story = {
  args: {
    title: 'Failed to load data',
    onRetry: () => alert('retry'),
    variant: 'compact',
  },
  parameters: {
    docs: {
      description: {
        story: '`message` is optional — title-only variant for brevity when the error is self-explanatory.',
      },
    },
  },
}
