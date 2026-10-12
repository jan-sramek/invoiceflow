#!/usr/bin/env bash
# Round trip through the S3 API: upload, read back and delete a test object.
set -euo pipefail

port="$1"
kubectl=(kubectl --context k3d-invoiceflow -n invoiceflow-infra)
secret() { "${kubectl[@]}" get secret seaweedfs-s3 -o jsonpath="{.data.$1}" | base64 -d; }
credentials="$(secret access-key):$(secret secret-key)"
url="http://127.0.0.1:$port/invoiceflow-documents/s3-check/$(date +%s).txt"
s3() { curl -sf --aws-sigv4 "aws:amz:us-east-1:s3" --user "$credentials" "$@"; }

"${kubectl[@]}" port-forward service/seaweedfs "$port:8333" >/dev/null &
forward=$!
trap 'kill $forward 2>/dev/null' EXIT
for _ in $(seq 1 20); do
  curl -s -o /dev/null "http://127.0.0.1:$port/healthz" && break
  sleep 0.5
done

expected="s3-check $(date -u +%FT%TZ)"
printf '%s' "$expected" | s3 -T - "$url" >/dev/null
actual="$(s3 "$url")"
s3 -X DELETE "$url" >/dev/null

if [[ "$actual" == "$expected" ]]; then
  echo "S3 round trip ok: uploaded, read back and deleted a test object in invoiceflow-documents"
else
  echo "S3 round trip failed: read back '$actual'" >&2
  exit 1
fi
