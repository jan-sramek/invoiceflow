#!/usr/bin/env bash
# Declares InvoiceFlow exchanges, quorum processing queue, TTL retry queues, and DLQ.
# Applications must not declare topology. Uses rabbitmqadmin (management plugin), not rabbitmqctl.
#
# WARNING: Queue arguments are immutable. If invoice-processing already exists as a classic
# queue (or with different args), delete it before re-running this script:
#   rabbitmqctl delete_queue --vhost=invoiceflow invoice-processing
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)
vhost=invoiceflow
admin_pass="$("${kubectl[@]}" -n invoiceflow-infra get secret rabbitmq-admin -o jsonpath='{.data.password}' | base64 -d)"

rabbitmqadmin() {
  "${kubectl[@]}" -n invoiceflow-infra exec -i rabbitmq-0 -- \
    rabbitmqadmin --username=admin --password="$admin_pass" --vhost="$vhost" "$@"
}

# Topic for new publishes (API outbox → worker)
rabbitmqadmin declare exchange name=invoiceflow.events type=topic durable=true
# Fanout-style topic for TTL retry staging (worker → retry queues)
rabbitmqadmin declare exchange name=invoiceflow.retry type=topic durable=true
# Dead-letter exchange for poison / exhausted messages
rabbitmqadmin declare exchange name=invoiceflow.dlx type=topic durable=true

# Quorum processing queue (survives node loss better than classic)
rabbitmqadmin declare queue name=invoice-processing durable=true \
  arguments='{"x-queue-type":"quorum"}'
rabbitmqadmin declare binding source=invoiceflow.events destination_type=queue \
  destination=invoice-processing routing_key=invoice.uploaded

# TTL retry queues: after expiry, messages dead-letter back to events / invoice.uploaded
declare_retry_queue() {
  local name="$1" ttl_ms="$2" routing_key="$3"
  rabbitmqadmin declare queue name="$name" durable=true \
    arguments="{\"x-message-ttl\":${ttl_ms},\"x-dead-letter-exchange\":\"invoiceflow.events\",\"x-dead-letter-routing-key\":\"invoice.uploaded\"}"
  rabbitmqadmin declare binding source=invoiceflow.retry destination_type=queue \
    destination="$name" routing_key="$routing_key"
}

declare_retry_queue invoice-processing.retry.10s 10000 retry.10s
declare_retry_queue invoice-processing.retry.1m 60000 retry.1m
declare_retry_queue invoice-processing.retry.5m 300000 retry.5m

rabbitmqadmin declare queue name=invoice-processing.dlq durable=true \
  arguments='{"x-queue-type":"quorum"}'
rabbitmqadmin declare binding source=invoiceflow.dlx destination_type=queue \
  destination=invoice-processing.dlq routing_key=invoice.dead

echo "RabbitMQ topology ready:"
echo "  invoiceflow.events → invoice-processing (quorum) / invoice.uploaded"
echo "  invoiceflow.retry → invoice-processing.retry.{10s,1m,5m} (TTL → events)"
echo "  invoiceflow.dlx → invoice-processing.dlq / invoice.dead"
echo "If declare failed with PRECONDITION_FAILED, delete the classic queue first (see script header)."
