#!/usr/bin/env bash
# Observability checks. Needs all services plus the monitoring stack:
#   (cd deploy && docker compose -f docker-compose.yml -f docker-compose.observability.yml up -d --build)
#   FakePaymentGateway__DeclineRate=0 FakePaymentGateway__TransientFailureRate=0 scripts/dev-up.sh
cd "$(dirname "$0")/.."; source scripts/lib-e2e.sh
JAEGER=${JAEGER:-http://localhost:16686}; PROM=${PROM:-http://localhost:9090}; SEQ=${SEQ:-http://localhost:5341}
ok() { echo "  PASS: $1"; }; bad() { echo "  FAIL: $1"; FAILED=1; }
expect() { [ "$2" = "$3" ] && ok "$1 = $3" || bad "$1: expected '$3' got '$2'"; }
prom() { curl -s -G "$PROM/api/v1/query" --data-urlencode "query=$1" | python3 -c "import sys,json;r=json.load(sys.stdin)['data']['result'];print(r[0]['value'][1] if r else 0)"; }

echo "1) Health endpoints on every service"
for entry in identity:5105 orders:5101 inventory:5102 payments:5103 notifications:5104 gateway:5100; do
  name=${entry%%:*}; port=${entry##*:}
  expect "$name live" "$(curl -s -o /dev/null -w '%{http_code}' localhost:$port/health/live)" 200
  expect "$name ready" "$(curl -s -o /dev/null -w '%{http_code}' localhost:$port/health/ready)" 200
done
expect "gateway sees all services (admin only)" "$(acurl $GATEWAY/health/dependencies | jget "['status']")" Healthy

echo "2) One order = one trace across services"
BEFORE_CREATED=$(prom 'sum(orderflow_orders_created_total)')
O=$(place_order $KEYBOARD 1); wait_for_state "$O" Confirmed 20 && ok "order confirmed"
sleep 8
python3 - "$JAEGER" <<'PY' && ok "trace spans gateway, orders, inventory, payments and notifications through RabbitMQ" || bad "cross-service trace"
import json,sys,urllib.request,urllib.parse
j=sys.argv[1]
def get(u): return json.load(urllib.request.urlopen(u))
traces=get(f"{j}/api/traces?service=gateway&limit=20&lookback=10m&tags="+urllib.parse.quote(json.dumps({"http.request.method":"POST"})))['data']
want={'gateway','orders-api','inventory-api','payments-api','notifications'}
for t in traces:
    procs={k:v['serviceName'] for k,v in t['processes'].items()}
    services={procs[s['processID']] for s in t['spans']}
    ops={s['operationName'] for s in t['spans']}
    if want<=services and any('receive' in o for o in ops) and any(o.startswith('Handle ') for o in ops):
        print(f"    trace {t['traceID'][:8]}: {len(t['spans'])} spans, services {sorted(services)}"); sys.exit(0)
sys.exit(1)
PY

echo "3) Business metrics reach Prometheus"
sleep 12
AFTER_CREATED=$(prom 'sum(orderflow_orders_created_total)')
[ "$(python3 -c "print(1 if float('$AFTER_CREATED')>float('$BEFORE_CREATED') else 0)")" = 1 ] && ok "orders_created went $BEFORE_CREATED -> $AFTER_CREATED" || bad "orders_created did not increase"
for metric in orderflow_orders_completed_total orderflow_payments_charges_total orderflow_stock_reservations_total orderflow_notifications_sent_total http_server_request_duration_seconds_count messaging_masstransit_consume_duration_milliseconds_count process_runtime_dotnet_gc_collections_count_total; do
  [ "$(prom "count($metric)")" != "0" ] && ok "$metric present" || bad "$metric missing"
done
expect "services reporting HTTP metrics" "$(prom 'count(count by (service_name) (http_server_request_duration_seconds_count))')" 6

echo "4) Logs are linked to traces"
curl -s "$SEQ/api/events?count=50" | python3 -c "
import sys,json
ev=json.load(sys.stdin)
n=sum(1 for e in ev if {'TraceId','CorrelationId'}<= {p['Name'] for p in e.get('Properties',[])})
sys.exit(0 if n>0 else 1)" && ok "recent log events carry TraceId and CorrelationId" || bad "log events carry TraceId and CorrelationId"

echo "5) Alert rules and dashboard are loaded"
expect "alert rules loaded" "$(curl -s $PROM/api/v1/rules | python3 -c "import sys,json;print(sum(len(g['rules']) for g in json.load(sys.stdin)['data']['groups']))")" 4
expect "Grafana dashboard" "$(curl -s -u admin:orderflow localhost:3000/api/dashboards/uid/orderflow | python3 -c "import sys,json;print(json.load(sys.stdin)['dashboard']['title'])")" "OrderFlow Overview"

[ -z "$FAILED" ] && echo "ALL PASSED" || { echo "SOME FAILED"; exit 1; }
