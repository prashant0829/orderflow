import { expect, type Page } from '@playwright/test'

export async function signInAs(page: Page, who: 'Customer' | 'Admin') {
  await page.goto('/login')
  await page.getByRole('button', { name: who, exact: true }).click()
  await expect(page.getByRole('navigation', { name: 'Main' })).toBeVisible()
}

/** The live connection is what makes the order page update by itself, so wait for it before placing orders. */
export async function waitForLive(page: Page) {
  await expect(page.getByText('Live').first()).toBeVisible()
}

export async function addToCart(page: Page, productName: string) {
  await page.getByRole('article', { name: productName }).getByRole('button', { name: /add to cart/i }).click()
}
