import { expect, test } from '@playwright/test'
import { signInAs } from './helpers'

test.describe('admin console', () => {
  test('shows the health of every service and the order counters', async ({ page }) => {
    await signInAs(page, 'Admin')
    await page.getByRole('link', { name: /admin/i }).click()

    await expect(page.getByRole('heading', { name: 'Admin console' })).toBeVisible()
    await expect(page.getByText('All systems normal')).toBeVisible()
    const services = page.getByRole('region', { name: 'Services' })
    for (const service of ['Identity', 'Inventory', 'Notifications', 'Orders', 'Payments']) {
      await expect(services.getByText(new RegExp(`^${service}$`, 'i'))).toBeVisible()
    }
    await expect(page.getByText('Failed messages', { exact: true }).first()).toBeVisible()
  })

  test('orders can be filtered by status', async ({ page }) => {
    await signInAs(page, 'Admin')
    await page.goto('/admin/orders')

    await page.getByRole('button', { name: 'Confirmed' }).click()

    await expect(page).toHaveURL(/status=Confirmed/)
    await expect(page.getByRole('button', { name: 'Confirmed' })).toHaveAttribute('aria-pressed', 'true')
  })

  test('stock can be added to a product', async ({ page }) => {
    await signInAs(page, 'Admin')
    await page.goto('/admin/inventory')

    const row = page.getByRole('row', { name: /Wireless Mouse/ })
    const before = Number(await row.locator('td').nth(2).locator('span').first().innerText())
    await row.getByLabel(/Quantity to add/).fill('5')
    await row.getByRole('button', { name: 'Restock' }).click()

    await expect(row.locator('td').nth(2).locator('span').first()).toHaveText(String(before + 5))
  })
})
