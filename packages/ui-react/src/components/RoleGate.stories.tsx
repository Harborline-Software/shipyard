import type { Meta, StoryObj } from '@storybook/react'
import { RoleGate } from './RoleGate'

const meta: Meta<typeof RoleGate> = {
  title: 'Auth/RoleGate',
  component: RoleGate,
  args: {
    role: 'owner',
    allow: ['owner', 'manager'],
    children: <span className="text-sm font-medium text-success">✓ Allowed content</span>,
    fallback: <span className="text-sm text-muted-foreground">Not authorized</span>,
  },
  argTypes: {
    role: { control: 'text' },
  },
}

export default meta
type Story = StoryObj<typeof RoleGate>

export const Allowed: Story = {
  args: { role: 'owner', allow: ['owner', 'manager'] },
  name: 'Role allowed — renders children',
}

export const Denied: Story = {
  args: { role: 'tenant', allow: ['owner', 'manager'] },
  name: 'Role denied — renders fallback',
}

export const DeniedNoFallback: Story = {
  args: { role: 'tenant', allow: ['owner', 'manager'], fallback: undefined },
  name: 'Role denied — no fallback (renders nothing)',
}

export const SingleRoleAllow: Story = {
  args: { role: 'owner', allow: ['owner'] },
  name: 'Single role allow list',
}
