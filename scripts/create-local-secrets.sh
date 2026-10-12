#!/usr/bin/env bash
# Creates the Secrets the local cluster needs, with random values. Existing Secrets are kept,
# because the services have already been initialised with their values.
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)

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
