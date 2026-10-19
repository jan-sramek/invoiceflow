#!/usr/bin/env bash
# Declares the InvoiceFlow exchange, queue and binding. Applications must not declare topology.
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)
vhost=invoiceflow

rabbitmqctl() { "${kubectl[@]}" -n invoiceflow-infra exec -i rabbitmq-0 -- rabbitmqctl "$@"; }

rabbitmqctl declare exchange --vhost="$vhost" name=invoiceflow.events type=topic durable=true auto_delete=false
rabbitmqctl declare queue --vhost="$vhost" name=invoice-processing durable=true auto_delete=false
rabbitmqctl declare binding --vhost="$vhost" source=invoiceflow.events destination_type=queue \
  destination=invoice-processing routing_key=invoice.uploaded

echo "RabbitMQ topology ready (invoiceflow.events → invoice-processing / invoice.uploaded)"
