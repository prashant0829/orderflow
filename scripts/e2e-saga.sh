#!/usr/bin/env bash
# End-to-end saga scenarios. Needs all services running with FakePaymentGateway random rates at 0:
#   FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 scripts/dev-up.sh
cd "$(dirname "$0")/.."; source scripts/lib-e2e.sh
ok() { echo "  PASS: $1"; }; bad() { echo "  FAIL: $1"; FAILED=1; }
expect() { [ "$2" = "$3" ] && ok "$1 = $3" || bad "$1: expected '$3' got '$2'"; }

echo "1) Happy path"
before=$(stock_of $KEYBOARD); O=$(place_order $KEYBOARD 2)
wait_for_state "$O" Confirmed 20 && ok "saga reached Confirmed"
expect "order status" "$(sleep 2; order_status $O)" Confirmed
expect "payment" "$(payment_status $O)" Completed
expect "stock reduced by 2" "$(stock_of $KEYBOARD)" "$((before-2))"

echo "2) Not enough stock"
O=$(place_order $MAT 99)
wait_for_state "$O" Failed 20 && ok "saga reached Failed"
expect "order status" "$(sleep 2; order_status $O)" Failed
expect "payment" "$(payment_status $O)" none

echo "3) Card declined (price 13.13) -> stock must be released"
P=$(new_product "Declined Widget" 13.13 5); O=$(place_order "$P" 1)
wait_for_state "$O" Failed 20 && ok "saga reached Failed"
expect "order status" "$(sleep 3; order_status $O)" Failed
expect "payment" "$(payment_status $O)" Failed
expect "stock released back to 5" "$(stock_of "$P")" 5

echo "4) Gateway outage (price 66.60) -> retries, then compensation"
P=$(new_product "Outage Widget" 66.60 5); O=$(place_order "$P" 1)
wait_for_state "$O" Failed 40 && ok "saga reached Failed"
expect "order status" "$(sleep 3; order_status $O)" Failed
expect "stock released back to 5" "$(stock_of "$P")" 5

# Note: scenario 4 trips the payment circuit breaker for 20 s; payments placed right after it fail fast (covered by unit tests).
[ -z "$FAILED" ] && echo "ALL PASSED" || { echo "SOME FAILED"; exit 1; }
