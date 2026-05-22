import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import { ErrorCard } from './ErrorCard'

describe('ErrorCard', () => {
  it('renders title', () => {
    render(<ErrorCard title="Failed to load data" />)
    expect(screen.getByText('Failed to load data')).toBeInTheDocument()
  })

  it('renders message when provided', () => {
    render(<ErrorCard title="Error" message="Network timeout" />)
    expect(screen.getByText('Network timeout')).toBeInTheDocument()
  })

  it('omits message when not provided', () => {
    render(<ErrorCard title="Error" />)
    expect(screen.queryByText(/message/i)).not.toBeInTheDocument()
  })

  it('renders retry button when onRetry is provided', () => {
    render(<ErrorCard title="Error" onRetry={() => {}} />)
    expect(screen.getByRole('button', { name: 'Retry' })).toBeInTheDocument()
  })

  it('omits retry button when onRetry is not provided', () => {
    render(<ErrorCard title="Error" />)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('calls onRetry when retry button is clicked', () => {
    const onRetry = vi.fn()
    render(<ErrorCard title="Error" onRetry={onRetry} />)
    fireEvent.click(screen.getByRole('button', { name: 'Retry' }))
    expect(onRetry).toHaveBeenCalledTimes(1)
  })

  it('has role=alert for screen readers', () => {
    render(<ErrorCard title="Error" />)
    expect(screen.getByRole('alert')).toBeInTheDocument()
  })

  it('renders page variant with h2 heading', () => {
    render(<ErrorCard title="Critical error" variant="page" />)
    expect(screen.getByRole('heading', { level: 2, name: 'Critical error' })).toBeInTheDocument()
  })

  it('renders default variant without heading', () => {
    render(<ErrorCard title="Failed to load" variant="default" />)
    expect(screen.queryByRole('heading')).not.toBeInTheDocument()
  })

  it('renders compact variant without heading', () => {
    render(<ErrorCard title="Failed to load" variant="compact" />)
    expect(screen.queryByRole('heading')).not.toBeInTheDocument()
  })
})
