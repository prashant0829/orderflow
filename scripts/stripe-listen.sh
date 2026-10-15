#!/usr/bin/env bash
# Forwards Stripe webhook events to your local gateway. Needs the Stripe CLI (brew install stripe/stripe-cli/stripe, then `stripe login`).
# The first line it prints contains the signing secret (whsec_...): put it in .env.stripe as Stripe__WebhookSecret and restart scripts/dev-up.sh.
# Without the webhook everything still works: the order page asks the server to re-check the payment right after you pay.
if ! command -v stripe >/dev/null 2>&1; then
  echo "Stripe CLI not found. Install it with: brew install stripe/stripe-cli/stripe" >&2
  exit 1
fi
exec stripe listen --forward-to "${GATEWAY:-http://localhost:5100}/api/v1/payments/webhooks/stripe" --events payment_intent.succeeded,payment_intent.payment_failed,payment_intent.canceled
