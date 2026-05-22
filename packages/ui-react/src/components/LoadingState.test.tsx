import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import { LoadingState } from './LoadingState'

describe('LoadingState', () => {
  it('renders label text', () => {
    render(<LoadingState label="Loading properties…" />)
    expect(screen.getByText('Loading properties…')).toBeInTheDocument()
  })

  it('page variant renders as a div (default)', () => {
    const { container } = render(<LoadingState label="Loading…" variant="page" />)
    expect(container.firstChild?.nodeName).toBe('DIV')
  })

  it('inline variant renders as a paragraph', () => {
    const { container } = render(<LoadingState label="Loading…" variant="inline" />)
    expect(container.firstChild?.nodeName).toBe('P')
  })

  it('defaults to page variant', () => {
    const { container } = render(<LoadingState label="Loading…" />)
    expect(container.firstChild?.nodeName).toBe('DIV')
  })

  it('inline variant has text-sm styling', () => {
    render(<LoadingState label="Loading payments…" variant="inline" />)
    const el = screen.getByText('Loading payments…')
    expect(el.className).toContain('text-sm')
  })
})
