import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Elements, PaymentElement, useElements, useStripe } from '@stripe/react-stripe-js'
import { loadStripe, type Stripe } from '@stripe/stripe-js'
import { CreditCard, Lock, ShieldCheck } from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { toast } from 'sonner'
import { ApiError } from '../../api/client'
import { paymentsApi } from '../../api/endpoints'
import type { Checkout } from '../../api/types'
import { formatMoney } from '../../lib/format'
import { invalidateForOrder } from '../../realtime/RealtimeProvider'
import { Button } from '../ui/Button'
import { Card } from '../ui/primitives'

const stripePromises = new Map<string, Promise<Stripe | null>>()

/** One Stripe.js instance per publishable key (loading it twice is wasteful and Stripe warns about it). */
function stripeFor(publishableKey: string) {
  let promise = stripePromises.get(publishableKey)
  if (!promise) {
    promise = loadStripe(publishableKey)
    stripePromises.set(publishableKey, promise)
  }
  return promise
}

const POLL_AFTER_PAYING_MS = 2000
const POLL_AFTER_PAYING_MAX = 30

/**
 * Shows the card form while the customer still has to pay. With the built-in fake payment provider there is nothing
 * to show: the payment finishes by itself and this component renders nothing.
 */
export function PaymentSection({ orderId, active }: { orderId: string; active: boolean }) {
  const checkout = useQuery({
    queryKey: ['checkout', orderId],
    queryFn: () => paymentsApi.checkout(orderId),
    enabled: active,
    // The payment is created a moment after the order (the saga asks for it), so a 404 just means "not yet".
    retry: (count, error) => (error instanceof ApiError && error.status === 404 ? count < 30 : count < 2),
    retryDelay: 1000,
    refetchInterval: (query) => (query.state.data?.status === 'AwaitingCustomer' ? false : 3000),
  })

  const data = checkout.data
  if (!active || !data || data.status !== 'AwaitingCustomer' || data.provider !== 'Stripe' || !data.publishableKey || !data.clientSecret) {
    return null
  }

  return <StripeCheckout orderId={orderId} checkout={data} publishableKey={data.publishableKey} clientSecret={data.clientSecret} />
}

function StripeCheckout({ orderId, checkout, publishableKey, clientSecret }: { orderId: string; checkout: Checkout; publishableKey: string; clientSecret: string }) {
  const stripe = useMemo(() => stripeFor(publishableKey), [publishableKey])
  const dark = document.documentElement.classList.contains('dark')
  const testMode = publishableKey.startsWith('pk_test_')

  return (
    <Card className="animate-slide-in border-brand-500/40 p-6" data-testid="payment-section">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 font-semibold">
          <CreditCard className="size-4 text-brand-500" /> Pay for your order
        </h2>
        <span className="text-lg font-semibold tabular-nums">{formatMoney(checkout.amount)}</span>
      </div>

      {testMode && (
        <p className="mb-4 rounded-xl bg-amber-500/10 px-3 py-2 text-xs text-amber-700 dark:text-amber-300">
          <strong>Test mode, no real money.</strong> Use card <span className="font-mono">4242 4242 4242 4242</span>, any future date and any CVC. To see a decline, use{' '}
          <span className="font-mono">4000 0000 0000 0002</span>.
        </p>
      )}

      <Elements
        stripe={stripe}
        options={{
          clientSecret,
          appearance: {
            theme: dark ? 'night' : 'stripe',
            variables: { colorPrimary: '#6366f1', borderRadius: '12px', fontFamily: 'Inter Variable, system-ui, sans-serif' },
          },
        }}
      >
        <PayForm orderId={orderId} />
      </Elements>

      <p className="mt-4 flex items-center gap-1.5 text-xs text-muted">
        <Lock className="size-3.5" /> Card details go straight to Stripe and never touch our servers.
        <ShieldCheck className="ml-1 size-3.5" /> Your items are held while you pay.
      </p>
    </Card>
  )
}

function PayForm({ orderId }: { orderId: string }) {
  const stripe = useStripe()
  const elements = useElements()
  const queryClient = useQueryClient()
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string>()
  const [waiting, setWaiting] = useState(false)
  const polls = useRef(0)

  // After paying we ask our server to re-check with Stripe every couple of seconds. The webhook usually gets there first,
  // but this way the page works without one (for example when developing locally).
  useEffect(() => {
    if (!waiting) return
    const timer = window.setInterval(async () => {
      polls.current += 1
      try {
        await paymentsApi.refresh(orderId)
      } catch {
        // Try again on the next tick.
      }
      invalidateForOrder(queryClient, orderId)
      if (polls.current >= POLL_AFTER_PAYING_MAX) setWaiting(false)
    }, POLL_AFTER_PAYING_MS)
    return () => window.clearInterval(timer)
  }, [waiting, orderId, queryClient])

  // A payment method that redirects (bank, wallet) brings the customer back with ?payment_intent=... in the address.
  useEffect(() => {
    if (new URLSearchParams(window.location.search).has('payment_intent')) setWaiting(true)
  }, [])

  async function pay(event: FormEvent) {
    event.preventDefault()
    if (!stripe || !elements) return

    setSubmitting(true)
    setError(undefined)
    const result = await stripe.confirmPayment({
      elements,
      redirect: 'if_required',
      confirmParams: { return_url: window.location.href.split('?')[0]! },
    })
    setSubmitting(false)

    if (result.error) {
      // A declined card is not the end of the order: the customer can fix the details or try another card.
      setError(result.error.message ?? 'The payment did not go through. Please try another card.')
      return
    }

    toast.success('Payment submitted', { description: 'Confirming with the bank...' })
    setWaiting(true)
    try {
      await paymentsApi.refresh(orderId)
    } catch {
      // The poll above keeps asking.
    }
    invalidateForOrder(queryClient, orderId)
  }

  return (
    <form onSubmit={pay} className="space-y-4">
      <PaymentElement />
      {error && (
        <p role="alert" className="rounded-xl bg-rose-500/10 px-3 py-2 text-sm text-rose-600 dark:text-rose-300">
          {error}
        </p>
      )}
      <Button type="submit" size="lg" className="w-full" loading={submitting || waiting} disabled={!stripe || !elements}>
        {waiting ? 'Confirming payment...' : 'Pay now'}
      </Button>
    </form>
  )
}
