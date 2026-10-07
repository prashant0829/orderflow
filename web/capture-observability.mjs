// Screenshots of the Grafana dashboard and a Jaeger trace for the README.
// Needs the monitoring stack and some recent traffic (see docs/RUNBOOK.md, "Observability").
import { chromium } from '@playwright/test'
import path from 'node:path'

const OUT = path.resolve('../docs/images')
const pause = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

const traces = (await (await fetch('http://localhost:16686/api/traces?service=gateway&limit=20&lookback=15m&operation=POST%20%2Fapi%2Fv1%2Forders%2F%7B**catch-all%7D')).json()).data
// Pick a trace that reaches every service, with a moderate number of spans so the picture stays readable.
const wanted = ['gateway', 'orders-api', 'inventory-api', 'payments-api', 'notifications']
const candidates = traces.filter((trace) => {
  const names = new Set(Object.values(trace.processes).map((process) => process.serviceName))
  return wanted.every((name) => names.has(name))
})
candidates.sort((a, b) => a.spans.length - b.spans.length)
const chosen = candidates[Math.floor(candidates.length / 2)] ?? traces[0]

const browser = await chromium.launch()
const context = await browser.newContext({ viewport: { width: 1440, height: 1500 }, colorScheme: 'dark' })
const page = await context.newPage()

await page.goto('http://localhost:3000/d/orderflow/orderflow-overview?orgId=1&from=now-15m&to=now&kiosk')
await pause(12000)
await page.screenshot({ path: path.join(OUT, 'grafana-dashboard.png') })

const trace = await browser.newContext({ viewport: { width: 1440, height: 900 }, colorScheme: 'dark' })
const tracePage = await trace.newPage()
await tracePage.goto(`http://localhost:16686/trace/${chosen.traceID}`)
await pause(6000)
await tracePage.screenshot({ path: path.join(OUT, 'jaeger-trace.png') })

await browser.close()
console.log(`trace ${chosen.traceID.slice(0, 8)} with ${chosen.spans.length} spans`)
