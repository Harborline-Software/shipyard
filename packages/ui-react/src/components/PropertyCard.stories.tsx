import type { Meta, StoryObj } from '@storybook/react'
import { PropertyCard } from './PropertyCard'

const meta: Meta<typeof PropertyCard> = {
  title: 'Domain/PropertyCard',
  component: PropertyCard,
  args: {
    name: 'PROP-0001',
    address: '150 Lexington Court',
    city: 'Seattle',
    state: 'WA',
    units: 4,
    status: 'Active',
  },
  argTypes: {
    status: {
      control: 'select',
      options: ['Active', 'Vacant', 'Maintenance', 'Sold'],
    },
    units: { control: { type: 'number', min: 0 } },
  },
}

export default meta
type Story = StoryObj<typeof PropertyCard>

export const Active: Story = { args: { status: 'Active' } }
export const Vacant: Story = { args: { status: 'Vacant' } }
export const Maintenance: Story = { args: { status: 'Maintenance' } }
export const Sold: Story = { args: { status: 'Sold' } }

export const SingleUnit: Story = {
  args: { units: 1, status: 'Active' },
  name: 'Single unit (pluralisation)',
}

export const WithActions: Story = {
  args: {
    status: 'Active',
    actions: (
      <div className="flex gap-2">
        <button className="text-xs text-primary hover:underline">View leases</button>
        <button className="text-xs text-primary hover:underline">Open detail</button>
      </div>
    ),
  },
}

export const Grid: Story = {
  render: () => (
    <div className="grid gap-4" style={{ gridTemplateColumns: 'repeat(3, 1fr)' }}>
      <PropertyCard name="PROP-0001" address="150 Lexington Court" city="Seattle" state="WA" units={4} status="Active" />
      <PropertyCard name="PROP-0002" address="22 Harborview Lane" city="Bellevue" state="WA" units={1} status="Vacant" />
      <PropertyCard name="PROP-0003" address="7 Marina Blvd" city="Kirkland" state="WA" units={8} status="Maintenance" />
    </div>
  ),
  name: 'Grid layout (3-up)',
}
