#!/usr/bin/env bash
# Send a MassTransit command to a queue through the RabbitMQ management API (handy for manual testing).
# Usage: scripts/send-command.sh <queue> <MessageTypeNamespace:Type> '<json>'
# Example: scripts/send-command.sh charge-payment OrderFlow.Contracts.Payments:ChargePayment '{"orderId":"...","customerId":"...","amount":10}'
set -euo pipefail
QUEUE=$1; URN=$2; JSON=$3
USER=${RABBITMQ_USER:-orderflow}; PASS=${RABBITMQ_PASSWORD:-orderflow_dev}
BODY=$(python3 -c "
import json,sys,uuid
envelope={'messageId':str(uuid.uuid4()),'messageType':['urn:message:$URN'],'message':json.loads(sys.argv[1])}
print(json.dumps({'properties':{'content_type':'application/vnd.masstransit+json','delivery_mode':2},'routing_key':'','payload':json.dumps(envelope),'payload_encoding':'string'}))
" "$JSON")
curl -s -u "$USER:$PASS" -H 'content-type: application/json' -X POST "http://localhost:15672/api/exchanges/%2F/$QUEUE/publish" -d "$BODY"; echo
