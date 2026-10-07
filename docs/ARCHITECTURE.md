# Architecture

```
React UI -> Gateway (YARP, JWT, rate limit) -> Orders API   (+ Identity for login)
                                                  |  (outbox)
                                                  v
                                              RabbitMQ  <-> Saga (in Orders)
                                         /        |         \
                                  Inventory   Payments    Notifications
                                     |           |
                                   SQL DBs (one per service)      Redis (cache, locks, rate limit)
```

## Happy path
1. `POST /orders` -> Order saved + `OrderSubmitted` written to outbox (same transaction).
2. Saga asks Inventory to reserve stock -> `StockReserved`.
3. Saga asks Payments to charge -> `PaymentCompleted`.
4. Order marked Confirmed -> Notifications sends email, SignalR pushes status to UI.

## Failure path
- Stock fails -> order Rejected.
- Payment fails -> saga tells Inventory to release stock -> order Failed.
- Payment times out -> same compensation.

_Detailed diagrams get added as phases complete._


## Messaging reliability (Phase 2)
```
POST /orders -> [SQL transaction: Orders + OutboxMessage] -> outbox delivery service -> RabbitMQ
                                                                                         |
                                  order-submitted queue -> Notifications consumer -> [SQL transaction: notification + InboxState]
                                        | after 5 failed retries
                                  order-submitted_error  --(admin replay, confirmed republish)--> order-submitted
```
- Outbox = nothing is lost if RabbitMQ is down. Inbox = nothing is processed twice. DLQ + replay = failures are visible and recoverable.


## Phase 3: command/reply pairs (the saga in Phase 4 will drive these)
| Command (queue) | Handler | Reply event(s) | Compensation |
|---|---|---|---|
| `ReserveStock` (reserve-stock) | Inventory | `StockReserved` / `StockReservationFailed` | `ReleaseStock` (release-stock) |
| `ChargePayment` (charge-payment) | Payments | `PaymentCompleted` / `PaymentFailed` | `RefundPayment` (refund-payment) -> `PaymentRefunded` |

Every consumer: idempotent by order id, inbox + outbox on its own database, retried then dead-lettered.


## Phase 4: the order saga (Orders service)
```
                 OrderSubmitted
   (initial) ------------------> AwaitingStock --StockReserved--> AwaitingPayment --PaymentCompleted--> Confirmed
                                     |  |                              |  |  |
                 StockReservationFailed  OrderCancelled               |  |  +-- PaymentTimeout --> Failed (release stock + refund)
                                     v            v                   |  +----- OrderCancelled --> Cancelled (release stock + refund)
                                  Failed       Cancelled              +-------- PaymentFailed ---> Failed (release stock)
```
Late results in Failed/Cancelled are compensated (StockReserved -> ReleaseStock, PaymentCompleted -> RefundPayment).
`GET /api/v1/orders/{id}/progress` shows the saga state; `GET /api/v1/orders/{id}` shows the order itself.


## Phase 5: real-time and notifications
```
Saga --OrderStatusChanged--> notifications-order-status-changed --> store timeline row --> SignalR hub --> browsers
Saga --OrderConfirmed / OrderFailed--> notifications-order-confirmed / -failed --> email (simulated) + history row
Orders --OrderSubmitted / OrderCancelled--> notifications-order-submitted / -cancelled --> email + history row
```
Browser flow (Phase 8): load `GET /timeline/{id}` once, connect to `/hubs/orders`, `JoinOrder(id)`, then append each `orderStatusChanged` push.


## Phase 6: security layout
```
Browser --HTTPS--> Gateway :5100  [correlation id -> security headers -> CORS -> JWT -> rate limit (Redis) -> route policy]
                      |-- /api/v1/auth/**            anonymous        -> Identity :5105
                      |-- /api/v1/orders/**          signed-in        -> Orders :5101
                      |-- /api/v1/notifications, timeline, /hubs/**   signed-in -> Notifications :5104
                      |-- /api/v1/stock (GET)        anonymous        -> Inventory :5102
                      |-- /api/v1/stock (POST), payments, admin/**    admin only
Every service re-validates the JWT (same signing key), so bypassing the gateway gains nothing.
```
Token flow: login -> access token (15 min, JWT with sub/role/name) + refresh token (7 days, rotated, stored hashed).


## Phase 7: observability
```
 6 services --OTLP traces+metrics--> OTel Collector --traces--> Jaeger  (:16686)
     |                                    '--metrics--> :8889 <--scrape-- Prometheus (:9090) --> Grafana (:3000) + alert rules
     |                                                                         ^
     |                                         RabbitMQ :15692 (queue depths, *_error) ---'
     '--Serilog (TraceId, CorrelationId)--> Seq (:5341)
```
- Trace context travels in HTTP headers and in MassTransit message headers (also through the outbox), so one order is one trace.
- Business metrics (`orderflow_*`) come from the services; transport metrics (`messaging_masstransit_*`) from MassTransit; `http_server_*` from ASP.NET; queue depth from RabbitMQ.


## Phase 8: the web UI
```
Browser (React SPA, :5173)
   |-- REST + JWT --------------> Gateway :5100 --> Identity / Orders / Inventory / Payments / Notifications
   '-- SignalR (WebSocket) -----> Gateway :5100 /hubs/orders --> Notifications (pushes OrderStatusChanged)
```
- Pages: Shop, My orders, Order tracking (stepper + live timeline), Inbox, Admin (overview, orders, failed messages, payments, inventory).
- The tracking page combines three reads (order, saga progress, timeline) with live pushes that simply invalidate those queries.
