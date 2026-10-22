#!/usr/bin/env bash
# Sends test traces from a pod through the node's collector to Tempo (and the Aspire Dashboard mirror).
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)
root="$(cd "$(dirname "$0")/.." && pwd)"

"${kubectl[@]}" apply -f "$root/deploy/kubernetes/namespace.yaml" >/dev/null
"${kubectl[@]}" -n invoiceflow delete job telemetry-check --ignore-not-found --wait=true >/dev/null
"${kubectl[@]}" apply -f "$root/deploy/observability/telemetry-check-job.yaml" >/dev/null

for _ in $(seq 1 60); do
  status="$("${kubectl[@]}" -n invoiceflow get job telemetry-check -o jsonpath='{.status.succeeded}/{.status.failed}')"
  case "$status" in
    1/*) echo "Sent 5 traces as service 'telemetry-check'; Grafana Explore → Tempo, or make dashboard-url (Aspire)"; exit 0 ;;
    */1) "${kubectl[@]}" -n invoiceflow logs job/telemetry-check >&2 || true; echo "telemetry-check failed" >&2; exit 1 ;;
  esac
  sleep 2
done

echo "telemetry-check timed out" >&2
exit 1
