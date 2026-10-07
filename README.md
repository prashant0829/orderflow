<h1 align="center">OrderFlow</h1>
<p align="center">
  An event-driven order platform that stays correct when things fail:<br/>
  saga with compensation, transactional outbox, idempotency everywhere, live order tracking, full observability.
</p>

<p align="center">
  <img alt=".NET 8" src="https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white">
  <img alt="SQL Server" src="https://img.shields.io/badge/SQL%20Server-2022-CC2927?logo=microsoftsqlserver&logoColor=white">
  <img alt="RabbitMQ" src="https://img.shields.io/badge/RabbitMQ-MassTransit-FF6600?logo=rabbitmq&logoColor=white">
  <img alt="Redis" src="https://img.shields.io/badge/Redis-cache%20%C2%B7%20rate%20limit-DC382D?logo=redis&logoColor=white">
  <img alt="React" src="https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=black">
  <img alt="TypeScript" src="https://img.shields.io/badge/TypeScript-7-3178C6?logo=typescript&logoColor=white">
  <img alt="Tailwind" src="https://img.shields.io/badge/Tailwind-4-06B6D4?logo=tailwindcss&logoColor=white">
  <img alt="YARP" src="https://img.shields.io/badge/YARP-gateway-512BD4">
  <img alt="SignalR" src="https://img.shields.io/badge/SignalR-live%20updates-512BD4">
  <img alt="OpenTelemetry" src="https://img.shields.io/badge/OpenTelemetry-Jaeger%20%C2%B7%20Prometheus%20%C2%B7%20Grafana-425CC7?logo=opentelemetry&logoColor=white">
  <img alt="Docker" src="https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white">
  <img alt="Playwright" src="https://img.shields.io/badge/Playwright-k6%20%C2%B7%20Testcontainers-45BA4B?logo=playwright&logoColor=white">
</p>

<p align="center"><img src="docs/images/demo.gif" alt="Placing an order and watching it complete live" width="860"></p>

## What it is

A small shop (catalog, cart, checkout, order tracking, admin console) built as six services around one hard problem: **an order touches stock, payment and notifications, each in its own database, and any step can fail or arrive late.** The project shows how to keep that consistent without distributed transactions, how to see what is happening, and how to prove it works.

- **6 services** (Gateway, Identity, Orders, Inventory, Payments, Notifications) + a React app, each service with its own database.
- **Orders are an orchestrated saga:** reserve stock, charge payment, confirm. If a step fails, the earlier steps are undone. Late results are compensated too.
- **Nothing is lost, nothing is done twice:** transactional outbox, inbox, idempotent consumers, retries, dead-letter queues you can replay from the UI.
- **Live UI:** the order page updates by itself over SignalR, with a polling safety net.
- **Observable:** one trace per order across every service and the message broker, business metrics, a Grafana dashboard, alerts.
- **Proven:** 169 automated tests including integration tests on real SQL Server / Redis / RabbitMQ containers, browser tests, and a load test that found (and fixed) a real bottleneck.

## See it

| | |
|---|---|
| ![Shop](docs/images/shop.png) **Shop**: stock badges, search, cart | ![Order in progress](docs/images/order-processing.png) **Live order tracking**: updates arrive by themselves |
| ![Failed order](docs/images/order-failed.png) **A failed order** says where it stopped and why; the stock is given back | ![Admin](docs/images/admin-overview.png) **Admin console**: service health, order counts, failed messages |
| ![Jaeger trace](docs/images/jaeger-trace.png) **One order = one trace** across 5 services and RabbitMQ (186 spans) | ![Grafana](docs/images/grafana-dashboard.png) **Dashboard**: orders, payments, stock, queues, latency |

<details>
<summary>More screens (light theme, phone, inbox, admin)</summary>

| | | |
|---|---|---|
| ![Light](docs/images/shop-light.png) | ![Mobile](docs/images/mobile-shop.png) | ![Mobile failed order](docs/images/mobile-order-failed.png) |
| ![Inbox](docs/images/inbox.png) | ![Admin orders](docs/images/admin-orders.png) | ![Admin inventory](docs/images/admin-inventory.png) |
| ![Login](docs/images/login.png) | ![Cart](docs/images/cart.png) | ![Admin payments](docs/images/admin-payments.png) |

</details>

## Architecture

```mermaid
flowchart LR
    Browser["React app"] -- "REST + JWT" --> GW["Gateway (YARP)<br/>JWT, rate limit, CORS"]
    Browser <-- "SignalR" --> GW
    GW --> ID["Identity<br/>(own DB)"]
    GW --> ORD["Orders<br/>saga orchestrator<br/>(own DB)"]
    GW --> INV["Inventory<br/>(own DB)"]
    GW --> PAY["Payments<br/>(own DB)"]
    GW --> NOT["Notifications<br/>live updates, emails<br/>(own DB)"]
    ORD -. "HTTP: price lookup" .-> INV
    ORD <--> MQ(("RabbitMQ"))
    INV <--> MQ
    PAY <--> MQ
    NOT <--> MQ
```

Every service has its own database and talks to the others only through messages (plus one read-only price lookup). Redis (not drawn) serves the gateway's rate limiting, the stock and order caches, the idempotency fast path and the SignalR backplane. Each .NET service follows Clean Architecture (Domain, Application, Infrastructure, Api).

### What happens when you place an order

```mermaid
sequenceDiagram
    actor C as Customer
    participant G as Gateway
    participant O as Orders
    participant Q as RabbitMQ
    participant I as Inventory
    participant P as Payments
    participant N as Notifications

    C->>G: POST /orders (Idempotency-Key)
    G->>O: forward (JWT checked twice)
    O->>I: look up prices (never trust the client)
    O->>O: save order + "submitted" message in ONE transaction (outbox)
    O-->>C: 201 Created
    O->>Q: outbox delivers OrderSubmitted
    Q->>O: saga starts, sends ReserveStock
    Q->>I: ReserveStock
    I-->>Q: StockReserved (atomic UPDATE, no overselling)
    Q->>O: saga sends ChargePayment, starts payment timeout
    Q->>P: ChargePayment
    P-->>Q: PaymentCompleted (retry + circuit breaker around the gateway)
    Q->>O: saga: Confirmed
    Q->>N: status events
    N-->>C: live update over SignalR + email
```

### The saga

```mermaid
stateDiagram-v2
    [*] --> AwaitingStock: OrderSubmitted
    AwaitingStock --> AwaitingPayment: StockReserved
    AwaitingStock --> Failed: no stock
    AwaitingStock --> Cancelled: customer cancels
    AwaitingPayment --> Confirmed: PaymentCompleted
    AwaitingPayment --> Failed: declined / gateway down / timeout
    AwaitingPayment --> Cancelled: customer cancels
    Confirmed --> Cancelled: cancel while confirmation in flight
    Failed --> [*]
    Cancelled --> [*]
    Confirmed --> [*]
```

On `Failed` and `Cancelled` the saga **releases the stock and refunds the payment**. A stock or payment result that arrives *after* the order already ended is compensated as well (stock released, payment refunded).

## Problems it solves

| Problem | How it is handled | Proof |
|---|---|---|
| The order is saved but the message is lost (or the reverse) | **Transactional outbox**: order and message commit in one SQL transaction, a relay delivers afterwards | Integration test pauses RabbitMQ, the order is still accepted and completes after it returns |
| A message is delivered twice | **Inbox** + handlers that are idempotent by order id (one reservation, one payment per order) | Duplicate-delivery unit tests, unique indexes |
| A customer double-clicks or the network retries | `Idempotency-Key` header: Redis fast path, SQL unique index as the safety net | 5 parallel requests with one key create exactly one order |
| Two customers buy the last item | **Atomic conditional update** (`... WHERE Available >= @n`), products locked in id order | 10 customers race for 3 items: exactly 3 win, never oversold |
| A payment provider is flaky | **Retry with backoff, circuit breaker, timeout** behind a decorator; failure becomes a compensated order | Unit tests for the resilience pipeline, circuit-open scenario |
| A step fails halfway across services | **Saga with compensation**, timeouts via delayed messages (survive restarts) | Slow-payment tests: timeout fails the order, the late payment is refunded |
| A message keeps failing | Retries, **dead-letter queue**, admin screen to inspect and replay (publisher-confirmed, never loses the message) | Admin UI flow, `e2e` scripts |
| Stolen refresh token | **Rotating refresh tokens with reuse detection** (reuse ends every session), hashed storage, account lockout, uniform login errors | Integration + security scripts |
| Brute force and abuse | Redis rate limiting (strict on login), CORS, security headers, JWT validated by every service | `scripts/e2e-security.sh` (9 groups of attacks) |
| "Why was this order slow?" | **OpenTelemetry**: one trace per order across HTTP, SQL and RabbitMQ; logs carry the trace id | Trace screenshot above |
| Does it hold under load? | k6 load test; it exposed a hot-row bottleneck (498 deadlocks), fixed it, and the same test now has 0 | [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md) |
| Browser needs live updates | SignalR with a Redis backplane, polling fallback, auto re-join after reconnect | Browser tests |
| Services stealing each other's messages | Queue names prefixed per service | Integration tests (queues never overlap) |

The design trade-offs (why a saga and not 2PC, why an outbox, why atomic stock updates, and so on) are explained in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) and [`docs/PERFORMANCE.md`](docs/PERFORMANCE.md).

## Tech stack

| Area | What |
|---|---|
| Backend | .NET 8, ASP.NET Core, Clean Architecture, CQRS with MediatR, FluentValidation, EF Core (+ atomic `ExecuteUpdate`) |
| Messaging | RabbitMQ, MassTransit (state machine saga, EF outbox/inbox, delayed messages, retries, DLQ) |
| Data | SQL Server 2022 (database per service), Redis (cache, idempotency, SignalR backplane, rate limiting) |
| Edge and security | YARP gateway, JWT access tokens + rotating refresh tokens, roles, per-route policies, rate limiting |
| Resilience | Polly 8 (retry, circuit breaker, timeout), HTTP resilience handler |
| Real time | SignalR (+ Redis backplane) |
| Frontend | React 19, TypeScript, Vite, Tailwind 4, TanStack Query, React Router, zustand |
| Observability | OpenTelemetry, Jaeger, Prometheus, Grafana (dashboard + alert rules), Serilog + Seq, health checks (live/ready) |
| Testing | xUnit, NSubstitute, MassTransit test harness, Testcontainers, Playwright, k6, Vitest + Testing Library |
| Delivery | Docker (one shared Dockerfile for all services), Docker Compose, GitHub Actions |

## Run it

**Everything in Docker** (needs about 6 GB of memory for Docker; the first build takes a few minutes):

```bash
cd deploy
cp .env.example .env
docker compose -f docker-compose.yml -f docker-compose.apps.yml up -d --build
```

Open **http://localhost:8081**. Sign in with the demo buttons (or `customer@orderflow.local` / `Customer#12345`, `admin@orderflow.local` / `Admin#12345`).
Add the monitoring stack (Grafana :3000, Jaeger :16686, Prometheus :9090) with `-f docker-compose.observability.yml` and `TELEMETRY_ENABLED=true`.

**Developing** (services run on your machine, infrastructure in Docker):

```bash
cd deploy && cp .env.example .env && docker compose up -d --build && cd ..
cd web && npm install && cd ..
dotnet build
scripts/dev-up.sh                # all services + the UI on http://localhost:5173
```

`make help` lists the common tasks. Ports, URLs, troubleshooting and every script are in [`docs/RUNBOOK.md`](docs/RUNBOOK.md).

## Tests

| Suite | Count | Run |
|---|---|---|
| .NET unit tests (domain, handlers, saga state machine, resilience, security) | 104 | `dotnet test --filter "FullyQualifiedName!~IntegrationTests"` |
| Integration tests: the real services on real SQL Server, Redis, RabbitMQ (Testcontainers) | 16 | `dotnet test tests/OrderFlow.IntegrationTests` |
| UI unit and component tests | 34 | `cd web && npm test` |
| Browser tests against the full system | 15 | `cd web && npx playwright test` |
| Scenario scripts (saga, security, observability) and k6 load test | 4 + 1 | `scripts/e2e-*.sh`, `scripts/load-test.sh` |

The build treats warnings as errors. CI ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)) builds, runs the suites above and builds all 7 Docker images.

## Project layout

```
src/
  BuildingBlocks/   shared kernel, CQRS pipeline, messaging, caching, security, observability, contracts
  Services/         Gateway, Identity, Orders, Inventory, Payments, Notifications (each: Domain / Application / Infrastructure / Api)
web/                React app, unit tests, Playwright tests
tests/              unit, integration (Testcontainers), load (k6)
deploy/             Docker Compose files, Dockerfiles, monitoring configs (Grafana dashboard, alert rules)
scripts/            start/stop, scenario tests, load test, demo-data reset
docs/               architecture, runbook, performance
```

## Documentation

[Architecture](docs/ARCHITECTURE.md) · [Runbook](docs/RUNBOOK.md) · [Performance](docs/PERFORMANCE.md)

## Honest limits

- **It is a demo.** Payments are simulated (a fake gateway with random failures), emails are stored instead of sent, and services run with the Development environment so they create their own databases and seed demo data.
- **The refresh token lives in `localStorage`**, which is the usual single-page-app compromise. Moving it to an httpOnly cookie behind one origin is the next hardening step.
- **Load numbers come from a memory-starved 8 GB laptop** running everything at once, so they are lower bounds. At 15 orders per second the saga queue backs up (and drains with nothing lost); at 8 per second everything stays under a second ([details](docs/PERFORMANCE.md)).
- **One hot product is one row.** A real flash sale would shard stock or reserve in Redis first.
- Not done: Alertmanager routing, HTTPS and asymmetric JWT keys, Kubernetes manifests, a real payment provider.

## License

MIT, see [LICENSE](LICENSE).
