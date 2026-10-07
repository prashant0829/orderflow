# OrderFlow web

React 19 + TypeScript + Vite + Tailwind 4 storefront and admin console. It only talks to the gateway.

```bash
npm install
npm run dev          # http://localhost:5173 (backend must be running: ../scripts/dev-up.sh)
npm run typecheck && npm test && npm run build
npx playwright test  # browser tests against the real system, see ../docs/RUNBOOK.md
```

Config: copy `.env.example` to `.env` to change the gateway URL (`VITE_API_URL`, default http://localhost:5100).
Structure: `src/api` (typed client with token refresh), `src/auth`, `src/cart`, `src/realtime` (SignalR), `src/components`, `src/pages`, `e2e` (Playwright).
Design notes: see `../docs/ARCHITECTURE.md`.
