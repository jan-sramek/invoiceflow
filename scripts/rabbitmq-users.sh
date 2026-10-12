#!/usr/bin/env bash
# One RabbitMQ user per application, with only the permissions it needs (NFR-SEC-12).
# Users that already exist are kept; passwords come from the Secrets in the invoiceflow namespace.
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)
vhost=invoiceflow

rabbitmqctl() { "${kubectl[@]}" -n invoiceflow-infra exec -i rabbitmq-0 -- rabbitmqctl "$@"; }
password() { "${kubectl[@]}" -n invoiceflow get secret "$1" -o jsonpath='{.data.password}' | base64 -d; }

# Usage: ensure_user <user> <secret> <configure> <write> <read> (permission regexes)
ensure_user() {
  local user="$1" secret="$2"
  if rabbitmqctl -q list_users | awk '{ print $1 }' | grep -qx "$user"; then
    echo "RabbitMQ user $user already exists"
  else
    # Read from stdin, so the password never shows up in process arguments. Hangs with -q.
    printf '%s\n' "$(password "$secret")" | rabbitmqctl add_user "$user" >/dev/null
    echo "RabbitMQ user $user created"
  fi
  rabbitmqctl -q set_permissions -p "$vhost" "$user" "$3" "$4" "$5"
}

# Neither application may declare or change topology
ensure_user invoiceflow-api rabbitmq-api '^$' '^invoiceflow\.events$' '^$'
ensure_user invoiceflow-worker rabbitmq-worker '^$' '^$' '^invoice-processing(\..+)?$'
