#!/usr/bin/env bash
# Creates the Secrets the local cluster needs, with random values. Existing Secrets are kept,
# because the services have already been initialised with their values.
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)
root="$(cd "$(dirname "$0")/.." && pwd)"

"${kubectl[@]}" apply -f "$root/deploy/kubernetes/infra-namespace.yaml" -f "$root/deploy/kubernetes/namespace.yaml" >/dev/null

# Usage: ensure_secret <namespace> <name> <key>... (every key gets its own random value)
ensure_secret() {
  local namespace="$1" name="$2"
  shift 2
  if "${kubectl[@]}" -n "$namespace" get secret "$name" >/dev/null 2>&1; then
    echo "Secret $namespace/$name already exists"
    return
  fi
  local literals=()
  for key in "$@"; do
    literals+=(--from-literal="$key=$(openssl rand -hex 24)")
  done
  "${kubectl[@]}" -n "$namespace" create secret generic "$name" "${literals[@]}" >/dev/null
  echo "Secret $namespace/$name created"
}

# Usage: copy_secret <namespace> <name> <target namespace> <key>... (keeps the name)
copy_secret() {
  local namespace="$1" name="$2" target="$3"
  shift 3
  if "${kubectl[@]}" -n "$target" get secret "$name" >/dev/null 2>&1; then
    echo "Secret $target/$name already exists"
    return
  fi
  local literals=()
  for key in "$@"; do
    literals+=(--from-literal="$key=$("${kubectl[@]}" -n "$namespace" get secret "$name" -o "jsonpath={.data.$key}" | base64 -d)")
  done
  "${kubectl[@]}" -n "$target" create secret generic "$name" "${literals[@]}" >/dev/null
  echo "Secret $target/$name created"
}

ensure_secret invoiceflow-infra postgres-superuser password
ensure_secret invoiceflow-infra seaweedfs-s3 access-key secret-key
ensure_secret invoiceflow-infra rabbitmq-admin password
ensure_secret invoiceflow-infra keycloak-db password
ensure_secret invoiceflow-infra keycloak-admin password
# Application credentials live next to the applications that use them
ensure_secret invoiceflow rabbitmq-api password
ensure_secret invoiceflow rabbitmq-worker password
ensure_secret invoiceflow catalog-app password
ensure_secret invoiceflow catalog-migrator password
# SeaweedFS in mini mode has a single identity, so the API shares it
copy_secret invoiceflow-infra seaweedfs-s3 invoiceflow access-key secret-key
