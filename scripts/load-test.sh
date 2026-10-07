#!/usr/bin/env bash
# Runs the k6 load test (tests/load/orders.js) in Docker against the running system.
# Needs: RateLimit__Enabled=false and FakePaymentGateway__* random rates at 0 when starting the services (see the script header).
# Usage: scripts/load-test.sh [PEAK_RATE_PER_SECOND]        e.g. scripts/load-test.sh 30
cd "$(dirname "$0")/.."
PEAK=${1:-20}
docker run --rm -i -e BASE_URL="${BASE_URL:-http://host.docker.internal:5100}" -e PEAK_RATE="$PEAK" grafana/k6 run - < tests/load/orders.js
