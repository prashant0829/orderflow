// Captures the screenshots and the demo GIF frames used in the README. Needs the whole system running:
//   FakePaymentGateway__LatencyMs=2500 FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 RateLimit__Auth__Limit=1000 scripts/dev-up.sh
// Usage (from web/): node scripts-capture.mjs
import { chromium } from '@playwright/test'
import fs from 'node:fs'
import path from 'node:path'

const BASE = process.env.E2E_BASE_URL ?? 'http://localhost:5173'
const API = process.env.VITE_API_URL ?? 'http://localhost:5100'
const OUT = path.resolve('../docs/images')
const FRAMES = '/tmp/frames'
fs.mkdirSync(OUT, { recursive: true })
fs.mkdirSync(FRAMES, { recursive: true })
for (const file of fs.readdirSync(FRAMES)) fs.rmSync(path.join(FRAMES, file))

const pause = (ms) => new Promise((resolve) => setTimeout(resolve, ms))
const shot = (page, name, options = {}) => page.screenshot({ path: path.join(OUT, `${name}.png`), ...options })

async function signIn(page, who, waitForLive = true) {
  await page.goto(`${BASE}/login`)
  await page.getByRole('button', { name: who, exact: true }).click()
  await page.getByRole('navigation', { name: 'Main' }).waitFor()
  if (waitForLive) await page.getByText('Live').first().waitFor()
}

async function apiToken(email, password) {
  const response = await fetch(`${API}/api/v1/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password }) })
  return (await response.json()).accessToken
}

const browser = await chromium.launch()
const dark = { viewport: { width: 1280, height: 800 }, colorScheme: 'dark' }

// ---------------------------------------------------------------- customer, dark, with the GIF frames
{
  const context = await browser.newContext(dark)
  const page = await context.newPage()
  await page.goto(`${BASE}/login`)
  await pause(600)
  await shot(page, 'login')

  await signIn(page, 'Customer')
  await page.getByRole('article').first().waitFor()
  await pause(800)
  await shot(page, 'shop')

  let frame = 0
  const snap = async () => page.screenshot({ path: path.join(FRAMES, `frame-${String(frame++).padStart(2, '0')}.png`) })

  await snap()
  await page.getByRole('article', { name: 'Mechanical Keyboard' }).getByRole('button', { name: /add to cart/i }).click()
  await page.getByRole('article', { name: 'Wireless Mouse' }).getByRole('button', { name: /add to cart/i }).click()
  await pause(500)
  await snap()
  await page.getByRole('button', { name: /open cart/i }).click()
  await pause(700)
  await shot(page, 'cart')
  await snap()
  await snap()

  await page.getByRole('button', { name: 'Place order' }).click()
  await page.waitForURL(/\/orders\/[0-9a-f-]{36}$/)
  const orderUrl = page.url()
  let confirmed = false
  for (let index = 0; index < 40 && (!confirmed || index < 40); index++) {
    await pause(350)
    await snap()
    if (index === 3) await shot(page, 'order-processing')
    confirmed = await page.getByText('Payment received. Your order is confirmed.').first().isVisible()
    if (confirmed) break
  }
  await pause(500)
  await snap()
  await snap()
  await shot(page, 'order-confirmed')
  fs.writeFileSync('/tmp/frames/order-url.txt', orderUrl)

  await page.goto(`${BASE}/notifications`)
  await pause(1200)
  await shot(page, 'inbox')

  // a failed order: more than there is in stock (the UI stops you, so place it through the API as the same customer)
  const token = await apiToken('customer@orderflow.local', 'Customer#12345')
  const stock = await (await fetch(`${API}/api/v1/stock`)).json()
  const mat = stock.find((item) => item.name.includes('Desk Mat'))
  const failed = await (
    await fetch(`${API}/api/v1/orders`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, 'Idempotency-Key': crypto.randomUUID() },
      body: JSON.stringify({ lines: [{ productId: mat.productId, quantity: 99 }] }),
    })
  ).json()
  await page.goto(`${BASE}/orders/${failed.id}`)
  await page.getByText('Stopped here').waitFor({ timeout: 30000 })
  await pause(500)
  await shot(page, 'order-failed')
  await page.goto(`${BASE}/orders`)
  await pause(1000)
  await shot(page, 'orders')
  fs.writeFileSync('/tmp/frames/failed-url.txt', `${BASE}/orders/${failed.id}`)
  await context.close()
}

// ---------------------------------------------------------------- light theme
{
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 }, colorScheme: 'light' })
  const page = await context.newPage()
  await signIn(page, 'Customer')
  await page.getByRole('article').first().waitFor()
  await pause(800)
  await shot(page, 'shop-light')
  await page.goto(fs.readFileSync('/tmp/frames/order-url.txt', 'utf8'))
  await page.getByText('Payment received. Your order is confirmed.').first().waitFor()
  await pause(800)
  await shot(page, 'order-light')
  await context.close()
}

// ---------------------------------------------------------------- phone
{
  const context = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, colorScheme: 'dark', isMobile: true, hasTouch: true })
  const page = await context.newPage()
  await signIn(page, 'Customer', false)
  await page.getByRole('article').first().waitFor()
  await pause(800)
  await shot(page, 'mobile-shop')
  await page.goto(fs.readFileSync('/tmp/frames/failed-url.txt', 'utf8'))
  await page.getByText('Stopped here').waitFor()
  await pause(600)
  await shot(page, 'mobile-order-failed')
  await context.close()
}

// ---------------------------------------------------------------- admin
{
  const context = await browser.newContext(dark)
  const page = await context.newPage()
  await signIn(page, 'Admin')
  for (const [route, name, wait] of [
    ['/admin', 'admin-overview', 'All systems normal'],
    ['/admin/orders', 'admin-orders', 'Order'],
    ['/admin/payments', 'admin-payments', 'Collected'],
    ['/admin/inventory', 'admin-inventory', 'New product'],
    ['/admin/dead-letters', 'admin-failed-messages', 'No failed messages'],
  ]) {
    await page.goto(`${BASE}${route}`)
    await page.getByText(wait, { exact: false }).first().waitFor({ timeout: 20000 })
    await pause(1200)
    await shot(page, name)
  }
  await context.close()
}

await browser.close()
console.log(`screenshots in ${OUT}, ${fs.readdirSync(FRAMES).filter((file) => file.endsWith('.png')).length} GIF frames in ${FRAMES}`)
