import { describe, expect, it } from 'vitest'
import { effectiveState, isFinal, stepStates } from './orderProgress'

describe('stepStates', () => {
  it('shows each stage as it is reached', () => {
    expect(stepStates('Submitted')).toEqual(['active', 'pending', 'pending', 'pending'])
    expect(stepStates('AwaitingStock')).toEqual(['done', 'active', 'pending', 'pending'])
    expect(stepStates('AwaitingPayment')).toEqual(['done', 'done', 'active', 'pending'])
    expect(stepStates('Confirmed')).toEqual(['done', 'done', 'done', 'done'])
  })

  it('marks the stock step as failed when the order never reached payment', () => {
    expect(stepStates('Failed', ['AwaitingStock', 'Failed'])).toEqual(['done', 'failed', 'pending', 'pending'])
  })

  it('marks the payment step as failed when payment had started', () => {
    expect(stepStates('Failed', ['AwaitingStock', 'AwaitingPayment', 'Failed'])).toEqual(['done', 'done', 'failed', 'pending'])
    expect(stepStates('Cancelled', ['AwaitingStock', 'AwaitingPayment', 'Cancelled'])).toEqual(['done', 'done', 'failed', 'pending'])
  })
})

describe('effectiveState', () => {
  it('prefers the saga state when there is one', () => {
    expect(effectiveState('AwaitingPayment', 'Pending')).toBe('AwaitingPayment')
  })

  it('falls back to the order status before the saga has started', () => {
    expect(effectiveState(undefined, 'Pending')).toBe('Submitted')
    expect(effectiveState(undefined, 'Confirmed')).toBe('Confirmed')
  })

  it('knows which states are final', () => {
    expect(isFinal('Confirmed')).toBe(true)
    expect(isFinal('Failed')).toBe(true)
    expect(isFinal('AwaitingStock')).toBe(false)
  })
})
