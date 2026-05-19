import type { Meta, StoryObj } from '@storybook/react'
import { SyncStateBadge } from './SyncStateBadge'

const meta: Meta<typeof SyncStateBadge> = {
  title: 'Sync/SyncStateBadge',
  component: SyncStateBadge,
  args: {
    state: 'synced',
  },
  argTypes: {
    state: {
      control: 'select',
      options: ['synced', 'syncing', 'pending', 'error', 'offline'],
    },
  },
}

export default meta
type Story = StoryObj<typeof SyncStateBadge>

export const Synced: Story = { args: { state: 'synced' } }
export const Syncing: Story = { args: { state: 'syncing' } }
export const Pending: Story = { args: { state: 'pending' } }
export const Error: Story = { args: { state: 'error' } }
export const Offline: Story = { args: { state: 'offline' } }

export const CustomLabel: Story = {
  args: { state: 'pending', label: '3 changes queued' },
}

export const AllStates: Story = {
  render: () => (
    <div className="flex flex-col gap-3">
      {(['synced', 'syncing', 'pending', 'error', 'offline'] as const).map((state) => (
        <SyncStateBadge key={state} state={state} />
      ))}
    </div>
  ),
}
