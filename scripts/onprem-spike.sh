#!/usr/bin/env bash
# Applies the on-prem Service/EndpointSlice and verifies TLS from a cluster pod (Stage 6 spike).
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
certs="$root/deploy/onprem/certs"
kubectl=(kubectl --context k3d-invoiceflow)
manifests="$root/deploy/kubernetes"

if ! docker ps --format '{{.Names}}' | grep -qx invoiceflow-onprem-postgres; then
  echo "On-prem PostgreSQL is not running (make onprem-up)" >&2
  exit 1
fi

if [[ ! -f "$certs/ca.crt" || ! -f "$certs/superuser.password" ]]; then
  echo "Missing $certs/ca.crt or superuser.password (make onprem-up)" >&2
  exit 1
fi

"${kubectl[@]}" apply -f "$manifests/namespace.yaml" -f "$manifests/onprem/service.yaml" >/dev/null

password="$(cat "$certs/superuser.password")"
"${kubectl[@]}" -n invoiceflow create secret generic onprem-postgres-superuser \
  --from-literal=password="$password" --dry-run=client -o yaml | "${kubectl[@]}" apply -f - >/dev/null

"${kubectl[@]}" -n invoiceflow create configmap onprem-postgres-ca \
  --from-file=ca.crt="$certs/ca.crt" --dry-run=client -o yaml | "${kubectl[@]}" apply -f - >/dev/null

"${kubectl[@]}" -n invoiceflow delete job onprem-postgres-check --ignore-not-found --wait=true >/dev/null
"${kubectl[@]}" apply -f "$manifests/onprem/check-job.yaml" >/dev/null

for _ in $(seq 1 60); do
  status="$("${kubectl[@]}" -n invoiceflow get job onprem-postgres-check -o jsonpath='{.status.succeeded}/{.status.failed}')"
  case "$status" in
    1/*)
      "${kubectl[@]}" -n invoiceflow logs job/onprem-postgres-check
      echo "onprem-spike: TLS verify-full from cluster to host Postgres succeeded"
      exit 0
      ;;
    */1)
      "${kubectl[@]}" -n invoiceflow logs job/onprem-postgres-check >&2 || true
      echo "onprem-spike failed" >&2
      exit 1
      ;;
  esac
  sleep 2
done

echo "onprem-spike timed out" >&2
exit 1
