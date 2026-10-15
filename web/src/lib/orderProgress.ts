import type { OrderStatus, ProgressState, TimelineEntry } from '../api/types'

export type StepState = 'done' | 'active' | 'pending' | 'failed'

export interface Step {
  key: 'received' | 'stock' | 'payment' | 'done'
  label: string
  hint: string
}

export const STEPS: Step[] = [
  { key: 'received', label: 'Order received', hint: 'We have your order' },
  { key: 'stock', label: 'Items reserved', hint: 'Checking and holding your items' },
  { key: 'payment', label: 'Payment', hint: 'Taking your payment' },
  { key: 'done', label: 'Confirmed', hint: 'All set' },
]

const ORDER: ProgressState[] = ['Submitted', 'AwaitingStock', 'AwaitingPayment', 'Confirmed']

/**
 * Turns the saga state into the state of each visual step.
 * For Failed/Cancelled the step where it stopped is marked failed; the timeline tells us how far the order got.
 */
export function stepStates(state: ProgressState, reached: ProgressState[] = []): StepState[] {
  switch (state) {
    case 'Submitted':
      return ['active', 'pending', 'pending', 'pending']
    case 'AwaitingStock':
      return ['done', 'active', 'pending', 'pending']
    case 'AwaitingPayment':
      return ['done', 'done', 'active', 'pending']
    case 'Confirmed':
      return ['done', 'done', 'done', 'done']
    case 'Failed':
    case 'Cancelled': {
      const reachedPayment = reached.includes('AwaitingPayment')
      return reachedPayment ? ['done', 'done', 'failed', 'pending'] : ['done', 'failed', 'pending', 'pending']
    }
  }
}

export const isFinal = (state: ProgressState) => state === 'Confirmed' || state === 'Failed' || state === 'Cancelled'

export const reachedStates = (timeline: TimelineEntry[]): ProgressState[] => timeline.map((entry) => entry.state)

/** The saga state is the source of truth for "where is it now"; fall back to the order status if the saga row is not there yet. */
export function effectiveState(progress: ProgressState | undefined, status: OrderStatus): ProgressState {
  if (progress) return progress
  return status === 'Pending' ? 'Submitted' : status
}

export const progressRank = (state: ProgressState) => ORDER.indexOf(state)
