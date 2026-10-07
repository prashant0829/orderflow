#!/usr/bin/env bash
# Wipes all application data (databases, Redis, RabbitMQ queues) and starts from a clean, seeded demo.
# Services recreate their databases and seed on the next start (Development only).
# Usage: scripts/reset-demo-data.sh            (then: scripts/dev-up.sh)
cd "$(dirname "$0")/.."
SQL_PASSWORD=${MSSQL_SA_PASSWORD:-OrderFlow_Dev#2026}
RABBIT_USER=${RABBITMQ_USER:-orderflow}; RABBIT_PASSWORD=${RABBITMQ_PASSWORD:-orderflow_dev}

scripts/dev-down.sh; sleep 2

for db in OrderFlow_Orders OrderFlow_Inventory OrderFlow_Payments OrderFlow_Notifications OrderFlow_Identity; do
  docker exec orderflow-sqlserver-1 /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$SQL_PASSWORD" \
    -Q "IF DB_ID('$db') IS NOT NULL BEGIN ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db]; END" >/dev/null && echo "dropped $db"
done

docker exec orderflow-redis-1 redis-cli FLUSHALL >/dev/null && echo "redis flushed"

for queue in $(curl -s -u "$RABBIT_USER:$RABBIT_PASSWORD" localhost:15672/api/queues | python3 -c "import sys,json;print(' '.join(q['name'] for q in json.load(sys.stdin)))"); do
  curl -s -u "$RABBIT_USER:$RABBIT_PASSWORD" -X DELETE "localhost:15672/api/queues/%2F/$queue" -o /dev/null
done
echo "rabbitmq queues deleted"
echo "Done. Start the system with scripts/dev-up.sh"
