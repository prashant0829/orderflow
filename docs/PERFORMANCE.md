# Performance and load testing

Run it: `RateLimit__Enabled=false FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 scripts/dev-up.sh`, then `scripts/load-test.sh [peak orders per second]`.
Script: `tests/load/orders.js` (k6 in Docker). Each virtual customer places an order through the gateway, then polls until the saga says Confirmed or Failed.

## Test machine (be honest about it)
MacBook with Apple M2, 8 cores, **8 GB RAM**. Everything ran on it at the same time: SQL Server (x64 image, emulated), RabbitMQ, Redis, Seq, six .NET services and the k6 load generator. The machine was swapping heavily (load average 30 to 48) and the project sits in an iCloud-synced folder. These are therefore **lower bounds**, useful for comparing before and after, not marketing numbers.

## Results (after the fix below)
| Peak rate | Orders | Create p95 | Create failures | Saga finished, p95 | Not finished |
|---|---|---|---|---|---|
| ramp to 4 orders/s | 324 | 40 ms | 0% | 0.56 s | 0 |
| ramp to 8 orders/s | 640 | 39 ms | 0% | 0.56 s | 0 |
| ramp to 15 orders/s | 904 | 1.5 s | 0% | backlog (see below) | all finished later |

"Saga finished" is measured by polling every 500 ms, so about 0.5 s of that is the poll interval; the real time is well under a second. At 15 orders/s the saga's event queue grew faster than one machine could drain it (about 5 orders/s with the host swapping). Nothing was lost: **all 904 orders ended Confirmed** about five minutes after the test, with zero errors, deadlocks or timeouts. The queue absorbed the spike, which is the point of putting RabbitMQ in front of the work.

## What the first run found (and how it was fixed)
Same test, before the fix (15 orders/s, one popular product):

| | Before | After |
|---|---|---|
| Order creation failures | 34% | 0% |
| Order creation p95 | 12.5 s | 1.5 s |
| Orders confirmed within the window | 25 | 240 |
| Inventory deadlocks / SQL timeouts | 498 / 523 | 0 / 0 |

Cause: every reservation loaded the stock row, changed it in memory and saved with optimistic concurrency. With many orders for the same product, almost every attempt collided with another, was retried with a delay, and collided again. Meanwhile the price lookup (a plain read) waited behind those writes.

Fix:
1. Take stock with one atomic statement, `UPDATE ... SET Available = Available - @n WHERE Id = @id AND Available >= @n`. Orders for a popular product now queue briefly on the row lock instead of retrying.
2. Always lock products in id order, so two orders can never deadlock each other.
3. Read-only lookups (prices, catalog) use `WITH (NOLOCK)`, so they never wait for reservations.
4. Undo explicitly: if the second line of an order fails, the first line is returned in the same transaction.

Side effect in the integration tests: the "ten customers race for three items" test went from 16 s to 2 s.

## Where the limits are (ideas, not done)
- Saga throughput is bound by the SQL round trips per event (saga state, inbox, outbox). More consumer instances, a faster database, or batching outbox delivery would raise it.
- A single hot product is a single row. For a real flash sale, shard the stock across several rows or reserve in Redis first.
- The test used one product at a time. A normal catalog spreads the load and avoids the row contention entirely.
