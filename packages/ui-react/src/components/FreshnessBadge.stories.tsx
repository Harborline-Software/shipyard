import type { Meta, StoryObj } from '@storybook/react'
import { FreshnessBadge } from './FreshnessBadge'

const meta: Meta<typeof FreshnessBadge> = {
  title: 'Sync/FreshnessBadge',
  component: FreshnessBadge,
  argTypes: {
    updatedAt: { control: 'date' },
    staleAfterMs: { control: { type: 'number', step: 60000 } },
  },
}

export default meta
type Story = StoryObj<typeof FreshnessBadge>

export const Fresh: Story = {
  args: {
    updatedAt: new Date(Date.now() - 30_000),
    staleAfterMs: 5 * 60 * 1000,
  },
}

export const Stale: Story = {
  args: {
    updatedAt: new Date(Date.now() - 10 * 60 * 1000),
    staleAfterMs: 5 * 60 * 1000,
  },
}

export const VeryOld: Story = {
  args: {
    updatedAt: new Date(Date.now() - 3 * 60 * 60 * 1000),
    staleAfterMs: 5 * 60 * 1000,
  },
}

export const CustomStaleThreshold: Story = {
  args: {
    updatedAt: new Date(Date.now() - 45_000),
    staleAfterMs: 30_000,
  },
  name: 'Custom stale threshold (30s)',
}
