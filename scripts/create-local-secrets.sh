#!/usr/bin/env bash
# Creates the Secrets the local cluster needs, with random values. Existing Secrets are kept,
# because the services have already been initialised with their values.
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)

ensure_secret() {
  local namespace="$1" name="$2" key="$3"
  if "${kubectl[@]}" -n "$namespace" get secret "$name" >/dev/null 2>&1; then
    echo "Secret $namespace/$name already exists"
  else
    "${kubectl[@]}" -n "$namespace" create secret generic "$name" \
      --from-literal="$key=$(openssl rand -hex 24)" >/dev/null
    echo "Secret $namespace/$name created"
  fi
}

ensure_secret invoiceflow-infra postgres-superuser password
