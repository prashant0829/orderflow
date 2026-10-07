import { Check, X } from 'lucide-react'
import type { ProgressState, TimelineEntry } from '../../api/types'
import { cn } from '../../lib/cn'
import { STEPS, reachedStates, stepStates, type StepState } from '../../lib/orderProgress'

function hintFor(defaultHint: string, stepState: StepState, state: ProgressState): string {
  if (stepState === 'failed') return state === 'Cancelled' ? 'Cancelled here' : 'Stopped here'
  const ended = state === 'Failed' || state === 'Cancelled'
  return ended && stepState === 'pending' ? 'Not reached' : defaultHint
}

/** The four visual steps of an order. The one in progress pulses; where an order stopped is shown in red. */
export function OrderStepper({ state, timeline }: { state: ProgressState; timeline: TimelineEntry[] }) {
  const states = stepStates(state, reachedStates(timeline))

  return (
    <ol className="grid gap-4 sm:grid-cols-4" aria-label="Order progress">
      {STEPS.map((step, index) => {
        const stepState = states[index]!
        const label = index === 3 && state === 'Cancelled' ? 'Cancelled' : index === 3 && state === 'Failed' ? 'Not completed' : step.label

        return (
          <li key={step.key} className="relative flex items-start gap-3 sm:flex-col sm:gap-2" aria-current={stepState === 'active' ? 'step' : undefined}>
            {index > 0 && (
              <span
                aria-hidden
                className={cn(
                  'absolute left-4 top-[-1rem] h-4 w-0.5 sm:left-[-50%] sm:top-4 sm:h-0.5 sm:w-full',
                  stepState === 'pending' ? 'bg-line' : stepState === 'failed' ? 'bg-rose-500/60' : 'bg-brand-500',
                )}
              />
            )}
            <span
              className={cn(
                'relative z-10 flex size-8 shrink-0 items-center justify-center rounded-full border-2 text-sm font-semibold transition-colors',
                stepState === 'done' && 'border-brand-500 bg-brand-500 text-white',
                stepState === 'active' && 'animate-pulse-ring border-brand-500 bg-surface text-brand-600 dark:text-brand-300',
                stepState === 'pending' && 'border-line bg-surface text-muted',
                stepState === 'failed' && 'border-rose-500 bg-rose-500 text-white',
              )}
            >
              {stepState === 'done' ? <Check className="size-4" /> : stepState === 'failed' ? <X className="size-4" /> : index + 1}
            </span>
            <div>
              <p className={cn('text-sm font-medium', stepState === 'pending' && 'text-muted')}>{label}</p>
              <p className="text-xs text-muted">{hintFor(step.hint, stepState, state)}</p>
            </div>
          </li>
        )
      })}
    </ol>
  )
}
