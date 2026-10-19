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

ensure_secret invoiceflow-infra postgres-superuser password
ensure_secret invoiceflow-infra seaweedfs-s3 access-key secret-key
ensure_secret invoiceflow-infra rabbitmq-admin password
ensure_secret invoiceflow-infra keycloak-db password
ensure_secret invoiceflow-infra keycloak-admin password
ensure_secret invoiceflow-infra keycloak-users clerk approver finance
# Application credentials live next to the applications that use them
ensure_secret invoiceflow rabbitmq-api password
ensure_secret invoiceflow rabbitmq-worker password
ensure_secret invoiceflow catalog-app password
ensure_secret invoiceflow catalog-migrator password

# The API reads document-storage; SeaweedFS was started with seaweedfs-s3. Same values.
if ! "${kubectl[@]}" -n invoiceflow get secret document-storage >/dev/null 2>&1; then
  access="$("${kubectl[@]}" -n invoiceflow-infra get secret seaweedfs-s3 -o jsonpath='{.data.access-key}' | base64 -d)"
  secret="$("${kubectl[@]}" -n invoiceflow-infra get secret seaweedfs-s3 -o jsonpath='{.data.secret-key}' | base64 -d)"
  "${kubectl[@]}" -n invoiceflow create secret generic document-storage \
    --from-literal=access-key="$access" --from-literal=secret-key="$secret" >/dev/null
  echo "Secret invoiceflow/document-storage created from invoiceflow-infra/seaweedfs-s3"
else
  echo "Secret invoiceflow/document-storage already exists"
fi
