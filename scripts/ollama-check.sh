#!/usr/bin/env bash
# Calls Ollama on the macOS host from a pod, through the selector-less Service.
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)
manifests="$(cd "$(dirname "$0")/.." && pwd)/deploy/kubernetes"

if ! curl -sf http://127.0.0.1:11434/api/version >/dev/null; then
  echo "Ollama is not running (make ollama-up)" >&2
  exit 1
fi

"${kubectl[@]}" apply -f "$manifests/namespace.yaml" -f "$manifests/ollama/service.yaml" >/dev/null
"${kubectl[@]}" -n invoiceflow delete job ollama-check --ignore-not-found --wait=true >/dev/null
"${kubectl[@]}" apply -f "$manifests/ollama/check-job.yaml" >/dev/null

for _ in $(seq 1 60); do
  status="$("${kubectl[@]}" -n invoiceflow get job ollama-check -o jsonpath='{.status.succeeded}/{.status.failed}')"
  case "$status" in
    1/*) "${kubectl[@]}" -n invoiceflow logs job/ollama-check | grep -o '"response":"[^"]*"'; exit 0 ;;
    */1) "${kubectl[@]}" -n invoiceflow logs job/ollama-check >&2 || true; echo "ollama-check failed" >&2; exit 1 ;;
  esac
  sleep 2
done

echo "ollama-check timed out" >&2
exit 1
