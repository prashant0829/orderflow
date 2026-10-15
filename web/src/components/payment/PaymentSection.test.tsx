import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { ReactNode } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../api/client'
import type { Checkout } from '../../api/types'
import { PaymentSection } from './PaymentSection'

const confirmPayment = vi.fn()

vi.mock('@stripe/stripe-js', () => ({ loadStripe: vi.fn(async () => ({})) }))
vi.mock('@stripe/react-stripe-js', () => ({
  Elements: ({ children }: { children: ReactNode }) => <div data-testid="elements">{children}</div>,
  PaymentElement: () => <div data-testid="payment-element" />,
  useStripe: () => ({ confirmPayment }),
  useElements: () => ({}),
}))
vi.mock('../../api/endpoints', () => ({ paymentsApi: { checkout: vi.fn(), refresh: vi.fn() } }))

const { paymentsApi } = await import('../../api/endpoints')

const waiting: Checkout = {
  status: 'AwaitingCustomer',
  amount: 129.49,
  provider: 'Stripe',
  publishableKey: 'pk_test_abc',
  clientSecret: 'pi_1_secret_x',
  currency: 'usd',
}

const renderSection = (active = true) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <PaymentSection orderId="order-1" active={active} />
    </QueryClientProvider>,
  )
}

describe('PaymentSection', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(paymentsApi.refresh).mockResolvedValue(waiting)
  })

  it('shows the card form, the amount and the test-card hint while the customer has to pay', async () => {
    vi.mocked(paymentsApi.checkout).mockResolvedValue(waiting)

    renderSection()

    expect(await screen.findByTestId('payment-element')).toBeInTheDocument()
    expect(screen.getByText('$129.49')).toBeInTheDocument()
    expect(screen.getByText(/Test mode, no real money/)).toBeInTheDocument()
    expect(screen.getByText('4242 4242 4242 4242')).toBeInTheDocument()
  })

  it('shows nothing when no payment is waiting (the built-in fake provider pays by itself)', async () => {
    vi.mocked(paymentsApi.checkout).mockResolvedValue({ ...waiting, status: 'Completed', clientSecret: null, provider: null, publishableKey: null })

    renderSection()

    await waitFor(() => expect(paymentsApi.checkout).toHaveBeenCalled())
    expect(screen.queryByTestId('payment-section')).not.toBeInTheDocument()
  })

  it('does not even ask while the order is not at the payment step', () => {
    renderSection(false)

    expect(paymentsApi.checkout).not.toHaveBeenCalled()
  })

  it('keeps asking while the payment has not been created yet (404), then shows the form', async () => {
    vi.mocked(paymentsApi.checkout).mockRejectedValueOnce(new ApiError(404, 'not found')).mockResolvedValue(waiting)

    renderSection()

    expect(await screen.findByTestId('payment-element', undefined, { timeout: 5000 })).toBeInTheDocument()
    expect(paymentsApi.checkout).toHaveBeenCalledTimes(2)
  })

  it('a declined card shows Stripe\'s message and does not tell the server anything happened', async () => {
    vi.mocked(paymentsApi.checkout).mockResolvedValue(waiting)
    confirmPayment.mockResolvedValue({ error: { message: 'Your card was declined.' } })
    renderSection()

    await userEvent.click(await screen.findByRole('button', { name: 'Pay now' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Your card was declined.')
    expect(paymentsApi.refresh).not.toHaveBeenCalled()
    expect(screen.getByRole('button', { name: 'Pay now' })).toBeEnabled()
  })

  it('after a successful payment it asks the server to re-check, without waiting for a webhook', async () => {
    vi.mocked(paymentsApi.checkout).mockResolvedValue(waiting)
    confirmPayment.mockResolvedValue({ paymentIntent: { status: 'succeeded' } })
    renderSection()

    await userEvent.click(await screen.findByRole('button', { name: 'Pay now' }))

    await waitFor(() => expect(paymentsApi.refresh).toHaveBeenCalledWith('order-1'))
    expect(confirmPayment).toHaveBeenCalledWith(expect.objectContaining({ redirect: 'if_required' }))
    expect(await screen.findByRole('button', { name: /Confirming payment/ })).toBeDisabled()
  })
})
