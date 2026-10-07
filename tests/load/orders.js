// Load test: customers place orders through the gateway and wait for the saga to finish.
//
// Run (the system must be running, with rate limiting and random payment failures off):
//   RateLimit__Enabled=false FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 scripts/dev-up.sh
//   scripts/load-test.sh              # or: docker run --rm -i grafana/k6 run - < tests/load/orders.js
//
// What it measures:
//   order_create      time for POST /orders to answer (the order is saved and the "submitted" message is in the outbox)
//   order_completion  time from sending the request until the saga says Confirmed or Failed (stock + payment + notifications, asynchronous)
import http from 'k6/http'
import { check, sleep } from 'k6'
import { Counter, Trend } from 'k6/metrics'
import { uuidv4 } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js'

const BASE_URL = __ENV.BASE_URL || 'http://host.docker.internal:5100'
const CUSTOMERS = Number(__ENV.CUSTOMERS || 10)
const PEAK_RATE = Number(__ENV.PEAK_RATE || 20) // new orders per second at the top of the ramp

const orderCreate = new Trend('order_create', true)
const orderCompletion = new Trend('order_completion', true)
const confirmed = new Counter('orders_confirmed')
const failed = new Counter('orders_failed')
const stuck = new Counter('orders_not_finished')

export const options = {
  scenarios: {
    orders: {
      executor: 'ramping-arrival-rate',
      startRate: 1,
      timeUnit: '1s',
      preAllocatedVUs: 60,
      maxVUs: 400,
      stages: [
        { target: Math.ceil(PEAK_RATE / 4), duration: '20s' },
        { target: Math.ceil(PEAK_RATE / 2), duration: '30s' },
        { target: PEAK_RATE, duration: '40s' },
        { target: PEAK_RATE, duration: '30s' },
        { target: 0, duration: '10s' },
      ],
    },
  },
  thresholds: {
    'http_req_failed{name:create}': ['rate<0.01'],
    order_create: ['p(95)<1000'],
    order_completion: ['p(95)<8000'],
    orders_not_finished: ['count==0'],
  },
}

const json = { headers: { 'Content-Type': 'application/json' } }
const authed = (token, extra = {}) => ({ headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, ...extra } })

function login(email, password) {
  const response = http.post(`${BASE_URL}/api/v1/auth/login`, JSON.stringify({ email, password }), json)
  if (response.status !== 200) throw new Error(`login failed for ${email}: ${response.status} ${response.body}`)
  return response.json('accessToken')
}

export function setup() {
  const admin = login('admin@orderflow.local', 'Admin#12345')

  // Plenty of stock so every order can succeed: the test measures throughput, not stock contention.
  const product = http.post(`${BASE_URL}/api/v1/stock`, JSON.stringify({ name: `Load test item ${Date.now()}`, unitPrice: 10, initialQuantity: 1000000 }), authed(admin))
  if (product.status !== 201) throw new Error(`could not create the product: ${product.status} ${product.body}`)

  const tokens = []
  for (let index = 0; index < CUSTOMERS; index++) {
    const response = http.post(
      `${BASE_URL}/api/v1/auth/register`,
      JSON.stringify({ email: `load${Date.now()}-${index}@example.com`, password: 'Passw0rd!x', displayName: `Load ${index}` }),
      json,
    )
    if (response.status !== 201) throw new Error(`register failed: ${response.status} ${response.body}`)
    tokens.push(response.json('accessToken'))
  }

  return { productId: product.json('productId'), tokens }
}

export default function (data) {
  const token = data.tokens[(__VU + __ITER) % data.tokens.length]
  const started = Date.now()

  const created = http.post(
    `${BASE_URL}/api/v1/orders`,
    JSON.stringify({ lines: [{ productId: data.productId, quantity: 1 }] }),
    { ...authed(token, { 'Idempotency-Key': uuidv4() }), tags: { name: 'create' } },
  )
  orderCreate.add(created.timings.duration)
  if (!check(created, { 'order accepted': (response) => response.status === 201 })) return

  const orderId = created.json('id')
  const deadline = started + 30000
  while (Date.now() < deadline) {
    sleep(0.5)
    const progress = http.get(`${BASE_URL}/api/v1/orders/${orderId}/progress`, { ...authed(token), tags: { name: 'progress' } })
    const state = progress.status === 200 ? progress.json('state') : ''
    if (state === 'Confirmed' || state === 'Failed') {
      orderCompletion.add(Date.now() - started)
      ;(state === 'Confirmed' ? confirmed : failed).add(1)
      return
    }
  }
  stuck.add(1)
}
