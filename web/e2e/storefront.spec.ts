import { expect, test } from '@playwright/test'
import { addToCart, signInAs, waitForLive } from './helpers'

test.describe('customer journey', () => {
  test('shopping, checkout and watching the order complete live', async ({ page }) => {
    await signInAs(page, 'Customer')
    await waitForLive(page)

    await expect(page.getByRole('heading', { name: 'Shop' })).toBeVisible()
    await addToCart(page, 'Wireless Mouse')
    await page.getByRole('button', { name: /open cart/i }).click()
    await expect(page.getByRole('dialog', { name: 'Shopping cart' })).toContainText('Wireless Mouse')
    await page.getByRole('button', { name: 'Place order' }).click()

    // The order page opens and updates by itself: no reload, no clicking.
    await expect(page).toHaveURL(/\/orders\/[0-9a-f-]{36}$/)
    await expect(page.getByText('Payment received. Your order is confirmed.')).toBeVisible({ timeout: 60_000 })
    await expect(page.getByLabel('Order progress').getByRole('listitem').first()).toBeVisible()
    await expect(page.getByRole('button', { name: 'Cancel order' })).toHaveCount(0)
    await expect(page.getByText('Confirmed', { exact: true }).first()).toBeVisible()
  })

  test('the confirmation email shows up in the inbox', async ({ page }) => {
    await signInAs(page, 'Customer')
    await page.getByRole('link', { name: /inbox/i }).click()

    await expect(page.getByRole('heading', { name: 'Inbox' })).toBeVisible()
    await expect(page.getByText(/is confirmed/i).first()).toBeVisible()
  })

  test('a reload keeps you signed in and keeps the cart', async ({ page }) => {
    await signInAs(page, 'Customer')
    await addToCart(page, 'Mechanical Keyboard')
    await expect(page.getByRole('button', { name: /open cart, 1 items/i })).toBeVisible()

    await page.reload()

    await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible()
    await expect(page.getByRole('button', { name: /open cart, 1 items/i })).toBeVisible()
  })

  test('products that are sold out cannot be added', async ({ page }) => {
    await signInAs(page, 'Customer')

    await page.route('**/api/v1/stock', (route) =>
      route.fulfill({
        json: [{ productId: 'p1', name: 'Ghost Product', unitPrice: 5, availableQuantity: 0, updatedAtUtc: new Date().toISOString() }],
      }),
    )
    await page.reload()

    await expect(page.getByText('Out of stock')).toBeVisible()
    await expect(page.getByRole('button', { name: /add to cart/i })).toBeDisabled()
  })

  test('theme choice survives a reload', async ({ page }) => {
    await signInAs(page, 'Customer')
    const html = page.locator('html')
    const wasDark = await html.evaluate((element) => element.classList.contains('dark'))

    await page.getByRole('button', { name: /switch to (light|dark) theme/i }).click()
    await page.reload()

    await expect(html).toHaveClass(wasDark ? /^((?!dark).)*$/ : /dark/)
  })
})

test.describe('access control', () => {
  test('anonymous visitors are sent to sign in', async ({ page }) => {
    await page.goto('/orders')

    await expect(page).toHaveURL(/\/login$/)
    await expect(page.getByRole('heading', { name: 'Welcome back' })).toBeVisible()
  })

  test('wrong credentials show a clear error', async ({ page }) => {
    await page.goto('/login')
    await page.getByLabel('Email').fill('nobody@example.com')
    await page.getByLabel('Password').fill('not-the-password')
    await page.getByRole('button', { name: 'Sign in' }).click()

    await expect(page.getByRole('alert')).toContainText('Invalid email or password')
  })

  test('customers do not get the admin console', async ({ page }) => {
    await signInAs(page, 'Customer')

    await expect(page.getByRole('link', { name: /admin/i })).toHaveCount(0)
    await page.goto('/admin')
    await expect(page.getByText('Admins only')).toBeVisible()
  })
})
