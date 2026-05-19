import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { CurrencyAmount } from './CurrencyAmount'

describe('CurrencyAmount', () => {
  it('formats USD amounts in en-US locale by default', () => {
    render(<CurrencyAmount amount={1500} />)
    expect(screen.getByText('$1,500.00')).toBeInTheDocument()
  })

  it('renders zero as $0.00', () => {
    render(<CurrencyAmount amount={0} />)
    expect(screen.getByText('$0.00')).toBeInTheDocument()
  })

  it('renders negative amounts with minus sign', () => {
    render(<CurrencyAmount amount={-250.5} />)
    expect(screen.getByText('-$250.50')).toBeInTheDocument()
  })

  it('accepts custom currency and locale', () => {
    render(<CurrencyAmount amount={1000} currency="EUR" locale="de-DE" />)
    const el = screen.getByText(/1\.000,00/)
    expect(el).toBeInTheDocument()
  })

  it('passes through html span props', () => {
    render(<CurrencyAmount amount={100} data-testid="price" className="font-bold" />)
    const el = screen.getByTestId('price')
    expect(el.tagName).toBe('SPAN')
    expect(el.className).toBe('font-bold')
  })
})
