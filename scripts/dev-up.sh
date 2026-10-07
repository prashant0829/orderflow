#!/usr/bin/env bash
# Start all services in the background (build first with `dotnet build`). Logs go to .logs/ (git-ignored).
# Extra env vars are passed through, e.g.:  OrderSaga__PaymentTimeoutSeconds=3 scripts/dev-up.sh
cd "$(dirname "$0")/.."
mkdir -p .logs
scripts/dev-down.sh >/dev/null 2>&1
for svc in "Identity/OrderFlow.Identity.Api" "Inventory/OrderFlow.Inventory.Api" "Payments/OrderFlow.Payments.Api" "Orders/OrderFlow.Orders.Api" "Notifications/OrderFlow.Notifications" "Gateway/OrderFlow.Gateway"; do
  name=$(basename "$svc")
  nohup dotnet run --project "src/Services/$svc" --no-build > ".logs/$name.log" 2>&1 < /dev/null &
done
# The UI (http://localhost:5173) starts too when its dependencies are installed (cd web && npm install).
if [ -d web/node_modules ]; then
  (cd web && nohup npm run dev > ../.logs/web.log 2>&1 < /dev/null &)
fi
echo "Starting... waiting for health"
for port in 5105 5101 5102 5103 5104 5100; do
  for _ in $(seq 1 40); do curl -sf "localhost:$port/health" >/dev/null && break; sleep 1; done
  echo "port $port: $(curl -s localhost:$port/health)"
done
[ -d web/node_modules ] && echo "UI: http://localhost:5173"
