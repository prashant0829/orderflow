#!/usr/bin/env bash
# Shared helpers for end-to-end scripts. Everything goes through the gateway, like a real client.
GATEWAY=${GATEWAY:-http://localhost:5100}
ORDERS=$GATEWAY; INVENTORY=$GATEWAY; PAYMENTS=$GATEWAY; NOTIFICATIONS=$GATEWAY
CUSTOMER=11111111-1111-1111-1111-111111111111
KEYBOARD=aaaaaaaa-0000-0000-0000-000000000001
MAT=aaaaaaaa-0000-0000-0000-000000000005

jget() { python3 -c "import sys,json;print(json.load(sys.stdin)$1)"; }

# login <email> <password> -> access token
login() {
  curl -s -X POST "$GATEWAY/api/v1/auth/login" -H 'Content-Type: application/json' -d "{\"email\":\"$1\",\"password\":\"$2\"}" | jget "['accessToken']"
}
CUSTOMER_TOKEN=$(login customer@orderflow.local 'Customer#12345')
ADMIN_TOKEN=$(login admin@orderflow.local 'Admin#12345')

# ccurl = curl as the demo customer, acurl = curl as the demo admin
ccurl() { curl -s -H "Authorization: Bearer $CUSTOMER_TOKEN" "$@"; }
acurl() { curl -s -H "Authorization: Bearer $ADMIN_TOKEN" "$@"; }

stock_of() { curl -s "$INVENTORY/api/v1/stock/$1" | jget "['availableQuantity']"; }
new_product() { acurl -X POST "$INVENTORY/api/v1/stock" -H 'Content-Type: application/json' -d "{\"name\":\"$1\",\"unitPrice\":$2,\"initialQuantity\":$3}" | jget "['productId']"; }

# place_order <productId> <qty>  -> prints order id (the customer comes from the token)
place_order() {
  ccurl -X POST "$ORDERS/api/v1/orders" -H 'Content-Type: application/json' -H "Idempotency-Key: e2e-$(uuidgen)" \
    -d "{\"lines\":[{\"productId\":\"$1\",\"quantity\":$2}]}" | jget "['id']"
}
progress() { ccurl "$ORDERS/api/v1/orders/$1/progress" | jget "['state']"; }
order_status() { ccurl "$ORDERS/api/v1/orders/$1" | jget "['status']"; }
payment_status() { acurl "$PAYMENTS/api/v1/payments/order/$1" | python3 -c "import sys,json;d=json.load(sys.stdin);print(d['status'] if 'orderId' in d else 'none')"; }

# wait_for_state <orderId> <state> [timeoutSeconds]
wait_for_state() {
  for _ in $(seq 1 "${3:-30}"); do [ "$(progress "$1")" = "$2" ] && return 0; sleep 1; done
  echo "TIMEOUT waiting for $2 (now $(progress "$1"))"; return 1
}
