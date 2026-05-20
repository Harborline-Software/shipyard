import type { Meta, StoryObj } from '@storybook/react'
import { CurrencyAmount } from './CurrencyAmount'

const meta: Meta<typeof CurrencyAmount> = {
  title: 'Primitives/CurrencyAmount',
  component: CurrencyAmount,
  args: {
    amount: 1234.56,
    currency: 'USD',
    locale: 'en-US',
  },
  argTypes: {
    amount: { control: { type: 'number', step: 0.01 } },
    currency: { control: 'text' },
    locale: { control: 'text' },
  },
}

export default meta
type Story = StoryObj<typeof CurrencyAmount>

export const Default: Story = {}

export const Zero: Story = {
  args: { amount: 0 },
}

export const Negative: Story = {
  args: { amount: -425.00 },
}

export const LargeAmount: Story = {
  args: { amount: 1_250_000 },
}

export const Euros: Story = {
  args: { amount: 9876.54, currency: 'EUR', locale: 'de-DE' },
}

export const BritishPounds: Story = {
  args: { amount: 5000, currency: 'GBP', locale: 'en-GB' },
}

export const WithClassName: Story = {
  args: { amount: 2500, className: 'font-semibold text-success' },
}
