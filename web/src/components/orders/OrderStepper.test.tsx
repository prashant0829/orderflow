import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { TimelineEntry } from '../../api/types'
import { OrderStepper } from './OrderStepper'

const entry = (state: TimelineEntry['state']): TimelineEntry => ({ state, message: state, occurredAtUtc: '2026-10-07T10:00:00Z' })

describe('OrderStepper', () => {
  it('marks the step in progress as current', () => {
    render(<OrderStepper state="AwaitingPayment" timeline={[entry('AwaitingStock'), entry('AwaitingPayment')]} />)

    const current = screen.getAllByRole('listitem').find((item) => item.getAttribute('aria-current') === 'step')
    expect(current).toHaveTextContent('Payment')
  })

  it('shows where a failed order stopped and what was never reached', () => {
    render(<OrderStepper state="Failed" timeline={[entry('AwaitingStock'), entry('Failed')]} />)

    expect(screen.getByText('Stopped here')).toBeInTheDocument()
    expect(screen.getAllByText('Not reached').length).toBeGreaterThan(0)
    expect(screen.queryByText('All set')).not.toBeInTheDocument()
  })

  it('labels the last step for a cancelled order', () => {
    render(<OrderStepper state="Cancelled" timeline={[entry('AwaitingStock'), entry('AwaitingPayment'), entry('Cancelled')]} />)

    expect(screen.getByText('Cancelled')).toBeInTheDocument()
    expect(screen.getByText('Cancelled here')).toBeInTheDocument()
  })
})
