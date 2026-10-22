#!/usr/bin/env bash
# Runs the migration Job, prints its log and fails if the Job fails.
# Usage: run-migrator.sh <image tag>
set -euo pipefail

tag="$1"
root="$(cd "$(dirname "$0")/.." && pwd)"
registry="${REGISTRY:-localhost:5001}"
kubectl=(kubectl --context k3d-invoiceflow)
app=("${kubectl[@]}" -n invoiceflow)

if ! "${app[@]}" get secret catalog-migrator >/dev/null 2>&1; then
  echo "The catalog has no credentials yet; run make db-up first" >&2
  exit 1
fi

postgres_host="$("${kubectl[@]}" -n invoiceflow-infra get pod postgres-0 -o jsonpath='{.status.podIP}')"
if [[ -z "$postgres_host" ]]; then
  echo "postgres-0 has no pod IP" >&2
  exit 1
fi
echo "Migrator Postgres host (postgres-0 pod IP): $postgres_host"

# A Job's pod template can't be changed, so every run replaces the previous Job
"${app[@]}" delete job migrator --ignore-not-found --wait >/dev/null
sed -e "s/\${IMAGE_TAG}/$tag/g" \
  -e "s|\${REGISTRY}|$registry|g" \
  -e "s|\${POSTGRES_HOST}|$postgres_host|g" \
  "$root/deploy/kubernetes/migrator/job.yaml" | "${app[@]}" apply -f - >/dev/null

deadline=$((SECONDS + 300))
until [[ "$("${app[@]}" get job migrator -o jsonpath='{.status.conditions[?(@.status=="True")].type}')" =~ (Complete|Failed) ]]; do
  if ((SECONDS > deadline)); then
    echo "The migration Job did not finish within 5 minutes" >&2
    exit 1
  fi
  sleep 2
done

"${app[@]}" logs job/migrator | jq -rR 'fromjson? | "\(.LogLevel) \(.Message)"'

if [[ "${BASH_REMATCH[1]}" == Failed ]]; then
  echo "Migration failed" >&2
  exit 1
fi
