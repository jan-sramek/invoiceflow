#!/usr/bin/env bash
# Runs the migration Job, prints its log and fails if the Job fails.
# Usage: run-migrator.sh <image tag>
set -euo pipefail

tag="$1"
root="$(cd "$(dirname "$0")/.." && pwd)"
kubectl=(kubectl --context k3d-invoiceflow -n invoiceflow)

if ! "${kubectl[@]}" get secret catalog-migrator >/dev/null 2>&1; then
  echo "The catalog has no credentials yet; run make db-up first" >&2
  exit 1
fi

# A Job's pod template can't be changed, so every run replaces the previous Job
"${kubectl[@]}" delete job migrator --ignore-not-found --wait >/dev/null
sed "s/\${IMAGE_TAG}/$tag/g" "$root/deploy/kubernetes/migrator/job.yaml" | "${kubectl[@]}" apply -f - >/dev/null

deadline=$((SECONDS + 300))
until [[ "$("${kubectl[@]}" get job migrator -o jsonpath='{.status.conditions[?(@.status=="True")].type}')" =~ (Complete|Failed) ]]; do
  if ((SECONDS > deadline)); then
    echo "The migration Job did not finish within 5 minutes" >&2
    exit 1
  fi
  sleep 2
done

"${kubectl[@]}" logs job/migrator | jq -rR 'fromjson? | "\(.LogLevel) \(.Message)"'

if [[ "${BASH_REMATCH[1]}" == Failed ]]; then
  echo "Migration failed" >&2
  exit 1
fi
