#!/usr/bin/env bash
# Adds a new RSA signing key for the invoiceflow realm (NFR-SEC-19 drill).
# Existing keys stay in JWKS until removed; tokens signed with the old key remain valid until expiry.
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)
app=("${kubectl[@]}" -n invoiceflow-infra)

admin_password() { "${app[@]}" get secret keycloak-admin -o jsonpath='{.data.password}' | base64 -d; }

pod="$("${app[@]}" get pod -l app.kubernetes.io/name=keycloak -o jsonpath='{.items[0].metadata.name}')"

kcadm() {
  "${app[@]}" exec -i "$pod" -- /opt/keycloak/bin/kcadm.sh "$@"
}

kcadm config credentials --server http://127.0.0.1:8080 --realm master \
  --user admin --password "$(admin_password)" >/dev/null

priority="$(date +%s)"
kcadm create keys -r invoiceflow \
  -s provider=rsa-generated \
  -s algorithm=RS256 \
  -s priority="$priority" >/dev/null

echo "New RS256 signing key created for realm invoiceflow (priority=$priority)."
echo "JWKS: https://auth.invoiceflow.localhost:8443/realms/invoiceflow/protocol/openid-connect/certs"
echo "Sign in again to obtain tokens signed with the new key; in-flight access tokens expire in 5 minutes (realm accessTokenLifespan)."
