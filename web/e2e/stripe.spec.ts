import { expect, test, type Page } from '@playwright/test'
import { addToCart, signInAs, waitForLive } from './helpers'

// These tests type real Stripe TEST cards into Stripe's hosted card form, so they need real Stripe test keys and internet access.
// They are skipped unless E2E_STRIPE=1. Start the system with Stripe test keys first (see docs/RUNBOOK.md, "Stripe test mode"):
//   cp .env.stripe.example .env.stripe   # fill in sk_test_... and pk_test_...
//   RateLimit__Auth__Limit=1000 scripts/dev-up.sh
//   cd web && E2E_STRIPE=1 npx playwright test e2e/stripe.spec.ts
test.skip(!process.env.E2E_STRIPE, 'Set E2E_STRIPE=1 and start the system with Stripe test keys.')

async function placeOrderAndOpenPaymentForm(page: Page) {
  await signInAs(page, 'Customer')
  await waitForLive(page)
  await addToCart(page, 'Wireless Mouse')
  await page.getByRole('button', { name: /open cart/i }).click()
  await page.getByRole('button', { name: 'Place order' }).click()
  await expect(page).toHaveURL(/\/orders\/[0-9a-f-]{36}$/)
  await expect(page.getByTestId('payment-section')).toBeVisible({ timeout: 45_000 })
  return page.frameLocator('iframe[title*="Secure payment input frame"]').first()
}

async function fillCard(card: ReturnType<Page['frameLocator']>, number: string) {
  await card.getByPlaceholder('1234 1234 1234 1234').fill(number)
  await card.getByPlaceholder('MM / YY').fill('12 / 34')
  await card.getByPlaceholder('CVC').fill('123')
}

test.describe('Stripe test mode', () => {
  test('paying with the 4242 test card confirms the order', async ({ page }) => {
    const card = await placeOrderAndOpenPaymentForm(page)

    await fillCard(card, '4242424242424242')
    await page.getByRole('button', { name: 'Pay now' }).click()

    await expect(page.getByText('Payment received. Your order is confirmed.').first()).toBeVisible({ timeout: 60_000 })
    await expect(page.getByTestId('payment-section')).toHaveCount(0)
  })

  test('a declined card shows the reason and the customer can try again', async ({ page }) => {
    const card = await placeOrderAndOpenPaymentForm(page)

    await fillCard(card, '4000000000000002')
    await page.getByRole('button', { name: 'Pay now' }).click()

    await expect(page.getByRole('alert')).toContainText(/declined/i, { timeout: 30_000 })
    await expect(page.getByRole('button', { name: 'Pay now' })).toBeEnabled()
    await expect(page.getByText('Payment received. Your order is confirmed.')).toHaveCount(0)
  })

  test('cancelling instead of paying cancels the payment too', async ({ page }) => {
    await placeOrderAndOpenPaymentForm(page)

    await page.getByRole('button', { name: 'Cancel order' }).click()
    await page.getByRole('button', { name: 'Cancel order' }).last().click()

    await expect(page.getByText('Order cancelled.').first()).toBeVisible({ timeout: 30_000 })
  })
})
