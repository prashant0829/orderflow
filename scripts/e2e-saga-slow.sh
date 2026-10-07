#!/usr/bin/env bash
# Timeout and cancel scenarios. Needs a slow payment gateway and a short saga timeout:
#   FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 FakePaymentGateway__LatencyMs=4000 \
#   OrderSaga__PaymentTimeoutSeconds=3 scripts/dev-up.sh
cd "$(dirname "$0")/.."; source scripts/lib-e2e.sh
ok() { echo "  PASS: $1"; }; bad() { echo "  FAIL: $1"; FAILED=1; }
expect() { [ "$2" = "$3" ] && ok "$1 = $3" || bad "$1: expected '$3' got '$2'"; }

echo "1) Payment takes longer than the saga timeout (3s vs 4s)"
P=$(new_product "Slow Widget" 25 5); O=$(place_order "$P" 1)
wait_for_state "$O" Failed 30 && ok "saga timed out into Failed"
expect "failure reason" "$(ccurl $ORDERS/api/v1/orders/$O/progress | jget "['failureReason']")" "Payment timed out."
expect "order status" "$(sleep 1; order_status $O)" Failed
echo "   (the late payment now completes and must be refunded automatically)"
for _ in $(seq 1 20); do [ "$(payment_status $O)" = "Refunded" ] && break; sleep 1; done
expect "late payment refunded" "$(payment_status $O)" Refunded
expect "stock released back to 5" "$(stock_of "$P")" 5

echo "2) Customer cancels while payment is in flight"
P=$(new_product "Cancel Widget" 30 5); O=$(place_order "$P" 1)
wait_for_state "$O" AwaitingPayment 20 && ok "saga is awaiting payment"
ccurl -X POST "$ORDERS/api/v1/orders/$O/cancel" -H 'Content-Type: application/json' -d '{"reason":"changed my mind"}' -o /dev/null -w "  cancel -> %{http_code}\n"
wait_for_state "$O" Cancelled 20 && ok "saga reached Cancelled"
expect "order status" "$(order_status $O)" Cancelled
for _ in $(seq 1 20); do [ "$(payment_status $O)" = "Refunded" ] && break; sleep 1; done
expect "late payment refunded" "$(payment_status $O)" Refunded
expect "stock released back to 5" "$(stock_of "$P")" 5

echo "3) Cancelling an already confirmed order is rejected by the domain rule"
echo "   (covered by unit tests: only Pending orders can be cancelled)"
[ -z "$FAILED" ] && echo "ALL PASSED" || { echo "SOME FAILED"; exit 1; }
