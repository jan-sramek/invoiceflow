#!/usr/bin/env bash
# Declares InvoiceFlow exchanges, quorum processing queue, TTL retry queues, and DLQ.
# Applications must not declare topology.
#
# WARNING: Queue arguments are immutable. If invoice-processing already exists as a classic
# queue (or with different args), delete it before re-running this script:
#   rabbitmqctl delete_queue --vhost=invoiceflow invoice-processing
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)
vhost=invoiceflow

rabbitmqctl() { "${kubectl[@]}" -n invoiceflow-infra exec -i rabbitmq-0 -- rabbitmqctl "$@"; }

# Topic for new publishes (API outbox → worker)
rabbitmqctl declare exchange --vhost="$vhost" name=invoiceflow.events type=topic durable=true auto_delete=false
# Fanout-style topic for TTL retry staging (worker → retry queues)
rabbitmqctl declare exchange --vhost="$vhost" name=invoiceflow.retry type=topic durable=true auto_delete=false
# Dead-letter exchange for poison / exhausted messages
rabbitmqctl declare exchange --vhost="$vhost" name=invoiceflow.dlx type=topic durable=true auto_delete=false

# Quorum processing queue (survives node loss better than classic)
rabbitmqctl declare queue --vhost="$vhost" name=invoice-processing durable=true auto_delete=false \
  arguments='{"x-queue-type":"quorum"}'
rabbitmqctl declare binding --vhost="$vhost" source=invoiceflow.events destination_type=queue \
  destination=invoice-processing routing_key=invoice.uploaded

# TTL retry queues: after expiry, messages dead-letter back to events / invoice.uploaded
declare_retry_queue() {
  local name="$1" ttl_ms="$2" routing_key="$3"
  rabbitmqctl declare queue --vhost="$vhost" name="$name" durable=true auto_delete=false \
    arguments="{\"x-message-ttl\":${ttl_ms},\"x-dead-letter-exchange\":\"invoiceflow.events\",\"x-dead-letter-routing-key\":\"invoice.uploaded\"}"
  rabbitmqctl declare binding --vhost="$vhost" source=invoiceflow.retry destination_type=queue \
    destination="$name" routing_key="$routing_key"
}

declare_retry_queue invoice-processing.retry.10s 10000 retry.10s
declare_retry_queue invoice-processing.retry.1m 60000 retry.1m
declare_retry_queue invoice-processing.retry.5m 300000 retry.5m

rabbitmqctl declare queue --vhost="$vhost" name=invoice-processing.dlq durable=true auto_delete=false \
  arguments='{"x-queue-type":"quorum"}'
rabbitmqctl declare binding --vhost="$vhost" source=invoiceflow.dlx destination_type=queue \
  destination=invoice-processing.dlq routing_key=invoice.dead

echo "RabbitMQ topology ready:"
echo "  invoiceflow.events → invoice-processing (quorum) / invoice.uploaded"
echo "  invoiceflow.retry → invoice-processing.retry.{10s,1m,5m} (TTL → events)"
echo "  invoiceflow.dlx → invoice-processing.dlq / invoice.dead"
echo "If declare failed with PRECONDITION_FAILED, delete the classic queue first (see script header)."
