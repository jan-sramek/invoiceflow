#!/usr/bin/env bash
# Generates a local CA and server certificate for the simulated on-premises PostgreSQL.
# Idempotent: existing files are kept. Output: deploy/onprem/certs/ (gitignored).
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
certs="$root/deploy/onprem/certs"
mkdir -p "$certs"

cn_api="onprem-postgres.invoiceflow.svc.cluster.local"

if [[ -f "$certs/ca.crt" && -f "$certs/server.crt" && -f "$certs/server.key" ]]; then
  echo "On-prem TLS certs already exist in $certs"
  exit 0
fi

openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
  -keyout "$certs/ca.key" -out "$certs/ca.crt" \
  -subj "/CN=InvoiceFlow OnPremises CA" >/dev/null 2>&1

openssl req -newkey rsa:4096 -sha256 -nodes \
  -keyout "$certs/server.key" -out "$certs/server.csr" \
  -subj "/CN=$cn_api" >/dev/null 2>&1

cat >"$certs/server.ext" <<EOF
subjectAltName=DNS:$cn_api,DNS:onprem-postgres,DNS:localhost,IP:127.0.0.1
extendedKeyUsage=serverAuth
EOF

openssl x509 -req -in "$certs/server.csr" -CA "$certs/ca.crt" -CAkey "$certs/ca.key" \
  -CAcreateserial -out "$certs/server.crt" -days 825 -sha256 -extfile "$certs/server.ext" >/dev/null 2>&1

chmod 600 "$certs/server.key" "$certs/ca.key"
rm -f "$certs/server.csr" "$certs/server.ext" "$certs/ca.srl"
echo "Wrote on-prem TLS material to $certs"
