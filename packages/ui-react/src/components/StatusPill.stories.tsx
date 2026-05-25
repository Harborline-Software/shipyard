import type { Meta, StoryObj } from '@storybook/react'
import { StatusPill } from './StatusPill'

const meta: Meta<typeof StatusPill> = {
  title: 'Status/StatusPill',
  component: StatusPill,
  argTypes: {
    kind: {
      control: 'select',
      options: ['glAccountType', 'occupancyStatus', 'agingBucket', 'balanceState', 'workOrderStatus'],
    },
    outlined: { control: 'boolean' },
  },
}

export default meta
type Story = StoryObj<typeof StatusPill>

// -------------------------------------------------------------------------
// GL Account Type
// -------------------------------------------------------------------------

export const GlAsset: Story = {
  args: { kind: 'glAccountType', value: 'Asset' },
  name: 'GL — Asset',
}

export const GlLiability: Story = {
  args: { kind: 'glAccountType', value: 'Liability' },
  name: 'GL — Liability',
}

export const GlEquity: Story = {
  args: { kind: 'glAccountType', value: 'Equity' },
  name: 'GL — Equity',
}

export const GlRevenue: Story = {
  args: { kind: 'glAccountType', value: 'Revenue' },
  name: 'GL — Revenue',
}

export const GlExpense: Story = {
  args: { kind: 'glAccountType', value: 'Expense' },
  name: 'GL — Expense',
}

// -------------------------------------------------------------------------
// Occupancy Status
// -------------------------------------------------------------------------

export const OccupancyOccupied: Story = {
  args: { kind: 'occupancyStatus', value: 'Occupied' },
  name: 'Occupancy — Occupied',
}

export const OccupancyNoticeGiven: Story = {
  args: { kind: 'occupancyStatus', value: 'NoticeGiven' },
  name: 'Occupancy — Notice Given',
}

export const OccupancyVacant: Story = {
  args: { kind: 'occupancyStatus', value: 'Vacant' },
  name: 'Occupancy — Vacant',
}

export const OccupancyOffMarket: Story = {
  args: { kind: 'occupancyStatus', value: 'OffMarket' },
  name: 'Occupancy — Off Market',
}

// -------------------------------------------------------------------------
// Aging Bucket
// -------------------------------------------------------------------------

export const AgingNoBalance: Story = {
  args: { kind: 'agingBucket', value: 'NoBalance' },
  name: 'Aging — No Balance',
}

export const AgingCurrent: Story = {
  args: { kind: 'agingBucket', value: 'Current' },
  name: 'Aging — Current',
}

export const Aging0To30: Story = {
  args: { kind: 'agingBucket', value: 'Days0To30' },
  name: 'Aging — 0-30 Days',
}

export const Aging31To60: Story = {
  args: { kind: 'agingBucket', value: 'Days31To60' },
  name: 'Aging — 31-60 Days',
}

export const Aging61To90: Story = {
  args: { kind: 'agingBucket', value: 'Days61To90' },
  name: 'Aging — 61-90 Days',
}

export const Aging90Plus: Story = {
  args: { kind: 'agingBucket', value: 'Days90Plus' },
  name: 'Aging — 90+ Days',
}

// -------------------------------------------------------------------------
// Balance State
// -------------------------------------------------------------------------

export const BalanceBalanced: Story = {
  args: { kind: 'balanceState', value: 'Balanced' },
  name: 'Balance — Balanced',
}

export const BalanceOutOfBalance: Story = {
  args: { kind: 'balanceState', value: 'OutOfBalance' },
  name: 'Balance — Out of Balance',
}

// -------------------------------------------------------------------------
// Work Order Status
// -------------------------------------------------------------------------

export const WorkOrderDraft: Story = {
  args: { kind: 'workOrderStatus', value: 'Draft' },
  name: 'Work Order — Draft',
}

export const WorkOrderSent: Story = {
  args: { kind: 'workOrderStatus', value: 'Sent' },
  name: 'Work Order — Sent',
}

export const WorkOrderAccepted: Story = {
  args: { kind: 'workOrderStatus', value: 'Accepted' },
  name: 'Work Order — Accepted',
}

export const WorkOrderScheduled: Story = {
  args: { kind: 'workOrderStatus', value: 'Scheduled' },
  name: 'Work Order — Scheduled',
}

export const WorkOrderInProgress: Story = {
  args: { kind: 'workOrderStatus', value: 'InProgress' },
  name: 'Work Order — In Progress',
}

export const WorkOrderCompleted: Story = {
  args: { kind: 'workOrderStatus', value: 'Completed' },
  name: 'Work Order — Completed',
}

export const WorkOrderOnHold: Story = {
  args: { kind: 'workOrderStatus', value: 'OnHold' },
  name: 'Work Order — On Hold',
}

export const WorkOrderCancelled: Story = {
  args: { kind: 'workOrderStatus', value: 'Cancelled' },
  name: 'Work Order — Cancelled',
}

// -------------------------------------------------------------------------
// Variants
// -------------------------------------------------------------------------

export const WithTooltip: Story = {
  args: { kind: 'occupancyStatus', value: 'Occupied', tooltip: 'Unit 101 — Active lease' },
  name: 'With Tooltip',
}

export const OutlinedVariant: Story = {
  args: { kind: 'glAccountType', value: 'Asset', outlined: true },
  name: 'Outlined variant',
}

export const UnknownValueFallback: Story = {
  args: { kind: 'workOrderStatus', value: 'Unknown' },
  name: 'Unknown value — fallback gray',
}
