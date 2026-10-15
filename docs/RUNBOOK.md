# Runbook

## Start / stop infrastructure
```bash
cd orderflow/deploy
cp .env.example .env        # first time only
docker compose up -d        # start
docker compose ps           # check health
docker compose down         # stop (keeps data); add -v to wipe data
```

## Ports
| Service | Host port | URL / connection |
|---|---|---|
| SQL Server | 1433 | `localhost,1433` user `sa`, password in `deploy/.env` |
| RabbitMQ | 5672 / 15672 | UI http://localhost:15672 (user/pass in `deploy/.env`) |
| Redis | **6380** (6379 is used by url-shortener project) | `localhost:6380` |
| Seq (logs) | 5341 | http://localhost:5341 |

## Build
```bash
cd orderflow && dotnet build
```

## Troubleshooting
- Port already allocated -> another project's container holds it (`docker ps`). Change host port in compose.
- SQL Server slow to start on Apple Silicon (runs via emulation): wait ~30-40s for healthy.

## Orders API
```bash
cd orderflow
dotnet build                                   # also applies analyzers
dotnet run --project src/Services/Orders/OrderFlow.Orders.Api   # http://localhost:5101
scripts/e2e-saga.sh                            # end-to-end checks through the gateway (see "Run everything")
dotnet test                                    # unit tests
```
- Swagger: http://localhost:5101/swagger · Health: http://localhost:5101/health
- Migrations apply automatically in Development. New migration:
  `dotnet ef migrations add <Name> -p src/Services/Orders/OrderFlow.Orders.Infrastructure -s src/Services/Orders/OrderFlow.Orders.Api -o Persistence/Migrations`
- If you add a migration, rebuild before `dotnet run --no-build` (stale binary = migration silently skipped).

## Notifications worker + messaging demo
```bash
cd orderflow
dotnet run --project src/Services/Orders/OrderFlow.Orders.Api &
dotnet run --project src/Services/Notifications/OrderFlow.Notifications &
```
- Dev config makes customer `99999999-9999-9999-9999-999999999999` a "poison" customer: its orders fail in the worker, retry ~15 s, then land in `notifications-order-submitted_error`.
- Dead letters: `GET /api/v1/admin/dead-letters` · `GET .../{queue}/messages` · `POST .../{queue}/replay`
- Start the worker without poison to "fix the bug": `Notifications__PoisonCustomerId= dotnet run --project src/Services/Notifications/OrderFlow.Notifications`, then replay.
- Outbox demo: `docker compose stop rabbitmq`, create an order (still 201), `docker compose start rabbitmq`, message is delivered.
- Queue stats in the management API lag ~5 s; `docker exec orderflow-rabbitmq-1 rabbitmqctl list_queues name messages` is instant.
- Databases: `OrderFlow_Orders`, `OrderFlow_Notifications` (auto-created and migrated in Development).

## Inventory (:5102) and Payments (:5103)
```bash
cd orderflow
dotnet run --project src/Services/Inventory/OrderFlow.Inventory.Api   # swagger: http://localhost:5102/swagger
dotnet run --project src/Services/Payments/OrderFlow.Payments.Api     # swagger: http://localhost:5103/swagger
```
- Seeded products (Development): ids `aaaaaaaa-0000-0000-0000-00000000000{1..5}`; #5 "Limited Edition Desk Mat" has only 3 in stock (good for failure demos).
- Payments Development config randomly declines 10% and fails 10% of calls. Turn off: `FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 dotnet run ...`
- Deterministic demo amounts: **13.13** always declined, **66.60** always gateway outage (retries, then trips the circuit breaker for 20 s; refunds fail fast meanwhile).
- Send a command by hand: `scripts/send-command.sh reserve-stock OrderFlow.Contracts.Inventory:ReserveStock '{"orderId":"<guid>","lines":[{"productId":"aaaaaaaa-0000-0000-0000-000000000001","quantity":2,"unitPrice":89.99}]}'`
- Event exchanges only exist after the first publish; to watch events, declare the exchange (durable fanout) and bind a temp queue to it in the RabbitMQ UI.
- New service: `scripts/scaffold-service.sh <Name>`.

## Ports
**Gateway 5100 (use this)** · Orders 5101 · Inventory 5102 · Payments 5103 · Notifications 5104 (REST + SignalR hub) · Identity 5105

## Run everything / saga scenarios
```bash
cd orderflow/deploy && docker compose up -d --build      # first time builds the RabbitMQ image with the delayed-message plugin
cd .. && dotnet build
FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 scripts/dev-up.sh   # all 4 services, logs in .logs/
scripts/e2e-saga.sh          # happy path, no stock, declined card, gateway outage
scripts/dev-down.sh
# timeout + cancel scenarios need slow payments and a short saga timeout:
FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 FakePaymentGateway__LatencyMs=4000 OrderSaga__PaymentTimeoutSeconds=3 scripts/dev-up.sh
scripts/e2e-saga-slow.sh
```
- Place an order: `POST /api/v1/orders` with header `Idempotency-Key` and body `{"customerId":"<guid>","lines":[{"productId":"aaaaaaaa-0000-0000-0000-000000000001","quantity":2}]}` (no price: it comes from Inventory).
- Watch it: `GET /api/v1/orders/{id}/progress`.

## Live updates (Phase 5)
```bash
cd orderflow/tools/signalr-watch && npm install        # once
node watch.mjs customerId=11111111-1111-1111-1111-111111111111 30   # watch every order of a customer for 30 s (or pass an order id)
```
- Notification history: `GET http://localhost:5104/api/v1/notifications?customerId=<guid>` · Timeline: `GET http://localhost:5104/api/v1/timeline/<orderId>`
- Emails are simulated: look for `EMAIL to ...` lines in `.logs/OrderFlow.Notifications.log` or Seq.
- Queue names are `<service>-<message>` (for example `notifications-order-status-changed`); dead-letter queues add `_error`.

## Security (Phase 6)
- Demo accounts (Development only, created by the Identity seeder): `customer@orderflow.local` / `Customer#12345` (id `1111...`), `admin@orderflow.local` / `Admin#12345`.
- Log in: `curl -X POST localhost:5100/api/v1/auth/login -H 'Content-Type: application/json' -d '{"email":"customer@orderflow.local","password":"Customer#12345"}'`, then send `Authorization: Bearer <accessToken>`. Access tokens last 15 min; use `/auth/refresh` with the refresh token.
- Browsers and scripts should go through the gateway (`:5100`). Swagger on each service has an Authorize button for the bearer token.
- Live feed: `TOKEN=<jwt> node tools/signalr-watch/watch.mjs me 30` (hub URL defaults to the gateway).
- Checks: `scripts/e2e-security.sh` (wait a minute after other scripts so the login rate limit window is clear).
- Real deployments: set `Jwt__SigningKey` (32+ bytes) and the `Seed__*` passwords as secrets; without seed passwords no demo accounts are created.
- Rate limit config: `RateLimit` section in the gateway appsettings; keys look like `ratelimit:auth:<ip>` in Redis (port 6380).

## Observability (Phase 7)
```bash
cd orderflow/deploy
docker compose -f docker-compose.yml -f docker-compose.observability.yml up -d --build   # adds collector, Jaeger, Prometheus, Grafana
cd .. && FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 scripts/dev-up.sh
scripts/e2e-observability.sh
```
| What | URL | Notes |
|---|---|---|
| Grafana dashboard | http://localhost:3000 | opens "OrderFlow Overview" (viewer access without login; admin / orderflow to edit) |
| Traces | http://localhost:16686 | pick service `gateway`, operation `POST /api/v1/orders/{**catch-all}`, open a trace |
| Metrics / alerts | http://localhost:9090 | Status > Targets, Alerts |
| Logs | http://localhost:5341 | filter by `TraceId` or `CorrelationId` to follow one request |
- Service health: `/health/live`, `/health/ready`, `/health` on each port; whole system: `GET http://localhost:5100/health/dependencies` with an admin token.
- Telemetry settings per service (`Telemetry` section): `Enabled`, `OtlpEndpoint` (default `http://localhost:4317`), `SampleRatio`, `MetricsIntervalSeconds`.
- Metrics are exported every 10 s; the dashboard refreshes every 10 s. Wait ~20 s after starting traffic before judging empty charts.
- Demo the alert: `Notifications__PoisonCustomerId=11111111-1111-1111-1111-111111111111 dotnet run --project src/Services/Notifications/OrderFlow.Notifications --no-build`, place an order, wait ~1 min: `DeadLetterQueueNotEmpty` fires. Fix and replay via `POST /api/v1/admin/dead-letters/<queue>/replay`.
- Dashboard JSON lives in `deploy/observability/grafana/dashboards/orderflow.json`; rebuild the grafana container after changing it.

## Web UI (Phase 8)
```bash
cd orderflow/web && npm install                      # once
cd .. && FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 scripts/dev-up.sh   # also starts the UI when web/node_modules exists
open http://localhost:5173
```
- Sign in with the "Customer" or "Admin" demo buttons (accounts from the Identity seeder), or register.
- Customer flow: add products -> cart -> Place order -> the order page updates live (Reserving, Payment, Confirmed). The Inbox shows the simulated emails.
- Admin flow: `/admin` shows service health and counters; "Failed messages" lets you inspect and replay dead letters; "Inventory" creates products and restocks.
- Make it interesting: order 10+ of "Limited Edition Desk Mat" (only 3 in stock) to see a failed order, or set the Payments env vars back to defaults (10% random declines/outages).
- Commands in `web/`: `npm run dev`, `npm run build`, `npm run typecheck`, `npm test`. Config: `web/.env.example` (copy to `.env` to change the gateway URL).
- Start from a clean demo (wipes databases, Redis, queues): `scripts/reset-demo-data.sh && scripts/dev-up.sh`.
- The UI origin must be listed in the gateway `Cors:AllowedOrigins` (default includes http://localhost:5173).

## Testing (Phase 9)
| What | Command | Needs |
|---|---|---|
| .NET unit tests (104) | `dotnet test --filter "FullyQualifiedName!~IntegrationTests"` | nothing |
| Integration tests (16) | `dotnet test tests/OrderFlow.IntegrationTests` | Docker; first start takes ~90 s (SQL Server under emulation on Apple Silicon); stop `scripts/dev-up.sh` first to save memory |
| UI unit tests (34) | `cd web && npm test` | nothing |
| UI type check | `cd web && npm run typecheck` | nothing |
| Browser tests (15) | `cd web && npx playwright test` | `FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 RateLimit__Auth__Limit=1000 scripts/dev-up.sh`; first time `npx playwright install chromium` |
| Load test | `scripts/load-test.sh 8` | `RateLimit__Enabled=false` plus no random payment failures when starting the services; results in `docs/PERFORMANCE.md` |
| Scenario scripts | `scripts/e2e-saga.sh`, `e2e-saga-slow.sh`, `e2e-security.sh`, `e2e-observability.sh` | running system |
- Open the browser test report after a failure: `cd web && npx playwright show-report`.
- Integration tests pause the RabbitMQ container with `docker pause` to simulate an outage, so the `docker` CLI must be on the PATH.
- If a test machine is short on memory (load average far above the core count), stop the monitoring containers first: `docker compose -f docker-compose.yml -f docker-compose.observability.yml stop grafana prometheus otel-collector jaeger`.
- Build an image locally (from the repository root): `docker build -f deploy/Dockerfile.dotnet --build-arg PROJECT=src/Services/Orders/OrderFlow.Orders.Api/OrderFlow.Orders.Api.csproj --build-arg ENTRY_DLL=OrderFlow.Orders.Api.dll -t orderflow-orders .` and `docker build -t orderflow-web web`.

## Everything in Docker (Phase 10)
```bash
cd deploy && cp .env.example .env            # first time
docker compose -f docker-compose.yml -f docker-compose.apps.yml up -d --build      # or: make docker-up
```
- UI: http://localhost:8081 · gateway: http://localhost:5100 · only these two are published.
- Needs about 6 GB for Docker. First start: SQL Server takes ~40 s (longer on Apple Silicon), services wait for it and restart if needed (`docker compose ... logs -f orders`).
- Options (set in `deploy/.env` or the shell): `PAYMENT_DECLINE_RATE`, `PAYMENT_OUTAGE_RATE` (try 0.1 to see compensation), `JWT_SIGNING_KEY`, `TELEMETRY_ENABLED=true` when the observability compose file is also used.
- Stop: `make docker-down` (keeps data). Wipe data: add `-v` to `docker compose ... down`.
- Regenerate README images: with the local stack running and payments slowed (`FakePaymentGateway__LatencyMs=2500`), `cd web && node capture-screenshots.mjs`, then build the GIF from `/tmp/frames`.

## Stripe test mode (real sandbox payments)
By default payments are simulated. To use Stripe's sandbox instead (a real card form, real PaymentIntents, no real money):
1. Create a free Stripe account and stay in **test mode**. Copy the two keys from https://dashboard.stripe.com/test/apikeys: `pk_test_...` (publishable) and `sk_test_...` (secret).
2. `cp .env.stripe.example .env.stripe` and fill in the two keys. The file is git-ignored; `scripts/dev-up.sh` loads it automatically (Docker: set `PAYMENT_PROVIDER=Stripe`, `STRIPE_SECRET_KEY`, `STRIPE_PUBLISHABLE_KEY` in `deploy/.env`).
3. Start as usual: `RateLimit__Auth__Limit=1000 scripts/dev-up.sh`. The Payments log says nothing special; check `.logs/OrderFlow.Payments.Api.log` if the service refuses to start (it fails fast when the provider is Stripe and the secret key is empty).
4. Place an order. On the order page a **Pay for your order** card appears with Stripe's card form. Use `4242 4242 4242 4242`, any future date, any CVC. `4000 0000 0000 0002` is always declined (the order stays open so you can try another card). Cancelling the order cancels the payment at Stripe; an order nobody pays is cancelled after the saga timeout (`OrderSaga__PaymentTimeoutSeconds`, default 900).
5. **Webhook (optional).** Out of the box the order page asks the server to re-check the payment right after you pay, so no webhook is needed locally. To exercise the real webhook path: install the Stripe CLI (`brew install stripe/stripe-cli/stripe`, `stripe login`), run `scripts/stripe-listen.sh`, put the `whsec_...` it prints in `.env.stripe` as `Stripe__WebhookSecret`, and restart `scripts/dev-up.sh`. In production you add `https://<your gateway>/api/v1/payments/webhooks/stripe` as a webhook endpoint in the Stripe dashboard (events `payment_intent.succeeded`, `payment_intent.payment_failed`, `payment_intent.canceled`).
6. See it at Stripe: https://dashboard.stripe.com/test/payments shows every PaymentIntent with the order id in its metadata. The admin Payments screen shows the same `pi_...` id as the reference.

Without keys, to try the webhook path locally: `scripts/stripe-test-webhook.sh <pi_id> [event_type]` sends a correctly signed fake event (secret `whsec_localtest`, or set `STRIPE_WEBHOOK_SECRET`).

Checks that need no Stripe account:
- Requests are valid against Stripe's real API specification: `docker run -d --rm -p 12111:12111 stripe/stripe-mock`, then `STRIPE_MOCK_URL=http://localhost:12111 dotnet test tests/OrderFlow.Payments.UnitTests --filter StripeMockContractTests`.
- Whole system against the mock: start the services with `Payments__Provider=Stripe Stripe__SecretKey=sk_test_mock Stripe__PublishableKey=pk_test_mock Stripe__ApiBase=http://localhost:12111 Stripe__WebhookSecret=whsec_localtest`. The order waits at Payment; confirm it with `scripts/stripe-test-webhook.sh`. (The card fields themselves only load with real keys, because Stripe's servers draw them.)
- Browser tests that type test cards into Stripe's form: `cd web && E2E_STRIPE=1 npx playwright test e2e/stripe.spec.ts` (needs real test keys; written but not yet run against real Stripe).
- Run the normal browser tests with the default Fake provider; they expect orders to confirm by themselves.
