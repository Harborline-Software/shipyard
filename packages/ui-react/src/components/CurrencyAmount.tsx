import { type HTMLAttributes } from 'react'

export interface CurrencyAmountProps extends Omit<HTMLAttributes<HTMLSpanElement>, 'children'> {
  amount: number
  currency?: string
  locale?: string
}

export function CurrencyAmount({ amount, currency = 'USD', locale = 'en-US', ...rest }: CurrencyAmountProps) {
  const formatted = new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount)
  return <span {...rest}>{formatted}</span>
}
