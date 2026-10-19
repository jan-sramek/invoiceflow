#!/usr/bin/env bash
# Creates a local CA and a certificate for auth.invoiceflow.localhost, then stores them
# as the TLS Secret Traefik uses for the Keycloak ingress. Existing Secret is kept.
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)
ns=invoiceflow-infra
secret=auth-invoiceflow-tls
dir="${INVOICEFLOW_TLS_DIR:-$HOME/.local/share/invoiceflow/tls}"
ca_key="$dir/ca.key"
ca_cert="$dir/ca.crt"
host_key="$dir/auth.invoiceflow.localhost.key"
host_cert="$dir/auth.invoiceflow.localhost.crt"

mkdir -p "$dir"

if [[ ! -f "$ca_cert" || ! -f "$ca_key" ]]; then
  openssl req -x509 -newkey rsa:4096 -sha256 -days 3650 -nodes \
    -keyout "$ca_key" -out "$ca_cert" \
    -subj "/CN=InvoiceFlow local CA" >/dev/null 2>&1
  echo "Local CA written to $dir"
fi

if [[ ! -f "$host_cert" || ! -f "$host_key" ]]; then
  openssl req -newkey rsa:2048 -nodes -keyout "$host_key" \
    -out "$dir/auth.csr" -subj "/CN=auth.invoiceflow.localhost" >/dev/null 2>&1
  openssl x509 -req -in "$dir/auth.csr" -CA "$ca_cert" -CAkey "$ca_key" -CAcreateserial \
    -out "$host_cert" -days 825 -sha256 \
    -extfile <(printf "subjectAltName=DNS:auth.invoiceflow.localhost") >/dev/null 2>&1
  rm -f "$dir/auth.csr"
  echo "Host certificate written to $dir"
fi

if "${kubectl[@]}" -n "$ns" get secret "$secret" >/dev/null 2>&1; then
  echo "Secret $ns/$secret already exists"
else
  "${kubectl[@]}" -n "$ns" create secret tls "$secret" \
    --cert="$host_cert" --key="$host_key" >/dev/null
  echo "Secret $ns/$secret created"
fi

echo "Trust the CA once on this Mac: make trust-local-ca"
echo "CA: $ca_cert"
