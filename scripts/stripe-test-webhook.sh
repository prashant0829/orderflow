#!/usr/bin/env bash
# Sends a correctly SIGNED fake Stripe webhook to the local gateway, so you can try the webhook path without the Stripe CLI.
# Usage: scripts/stripe-test-webhook.sh <payment_intent_id> [event_type]
#   scripts/stripe-test-webhook.sh pi_3Abc...                         # payment_intent.succeeded
#   scripts/stripe-test-webhook.sh pi_3Abc... payment_intent.payment_failed
# The id is the "Reference" of the payment in the admin Payments screen. The secret must match Stripe__WebhookSecret of the Payments
# service (default for this script: whsec_localtest). With real Stripe test mode you normally use `scripts/stripe-listen.sh` instead.
set -euo pipefail
PAYMENT_ID=${1:?usage: $0 <payment_intent_id> [event_type]}
TYPE=${2:-payment_intent.succeeded}
SECRET=${STRIPE_WEBHOOK_SECRET:-whsec_localtest}
URL=${GATEWAY:-http://localhost:5100}/api/v1/payments/webhooks/stripe
TIMESTAMP=$(date +%s)
BODY=$(printf '{"id":"evt_local_%s","object":"event","api_version":"2020-08-27","created":%s,"livemode":false,"type":"%s","data":{"object":{"id":"%s","object":"payment_intent","status":"succeeded","amount":1000,"currency":"usd"}}}' "$TIMESTAMP" "$TIMESTAMP" "$TYPE" "$PAYMENT_ID")
SIGNATURE=$(printf '%s.%s' "$TIMESTAMP" "$BODY" | openssl dgst -sha256 -hmac "$SECRET" | awk '{print $NF}')
curl -s -o /dev/null -w "webhook $TYPE for $PAYMENT_ID -> HTTP %{http_code}\n" -X POST "$URL" \
  -H "Content-Type: application/json" -H "Stripe-Signature: t=$TIMESTAMP,v1=$SIGNATURE" -d "$BODY"
