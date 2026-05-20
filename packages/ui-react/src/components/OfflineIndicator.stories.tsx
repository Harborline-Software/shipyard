import type { Meta, StoryObj } from '@storybook/react'
import { OfflineIndicator } from './OfflineIndicator'

const meta: Meta<typeof OfflineIndicator> = {
  title: 'Sync/OfflineIndicator',
  component: OfflineIndicator,
  parameters: {
    docs: {
      description: {
        component:
          'Listens to `navigator.onLine` and renders an alert banner when offline. Renders nothing when online. Use Storybook\'s network throttle or the story `ForceVisible` below to preview the offline state.',
      },
    },
  },
}

export default meta
type Story = StoryObj<typeof OfflineIndicator>

export const Default: Story = {}

export const CustomMessage: Story = {
  args: {
    message: 'No internet connection. Data shown may be out of date.',
  },
}

export const ForceVisible: Story = {
  render: (args) => (
    <div
      role="alert"
      className="flex items-center gap-2 rounded-md px-4 py-2 text-sm"
      style={{
        background: 'color-mix(in srgb, #d97706 10%, transparent)',
        border: '1px solid color-mix(in srgb, #d97706 20%, transparent)',
        color: '#d97706',
      }}
    >
      {args.message ?? 'You are offline. Changes will sync when reconnected.'}
    </div>
  ),
  name: 'Force visible (design preview)',
  args: { message: undefined },
}
