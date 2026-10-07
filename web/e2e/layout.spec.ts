import { expect, test } from '@playwright/test'
import { signInAs } from './helpers'

test.describe('small screens', () => {
  test('the shop and the top bar fit without sideways scrolling', async ({ page }) => {
    await signInAs(page, 'Customer')
    await expect(page.getByRole('heading', { name: 'Shop' })).toBeVisible()

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
    expect(overflow).toBeLessThanOrEqual(0)
    await expect(page.getByRole('button', { name: /open cart/i })).toBeInViewport()
    await expect(page.getByRole('button', { name: 'Account menu' })).toBeInViewport()
  })

  test('the admin tables scroll inside their card instead of breaking the page', async ({ page }) => {
    await signInAs(page, 'Admin')
    await page.goto('/admin/payments')
    await expect(page.getByRole('heading', { name: 'Admin console' })).toBeVisible()

    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
    expect(overflow).toBeLessThanOrEqual(0)
  })
})
