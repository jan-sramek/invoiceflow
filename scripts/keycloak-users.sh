#!/usr/bin/env bash
# Sets the three development persona passwords from the keycloak-users Secret.
# Safe to re-run; needs Keycloak up and the realm imported.
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)
app=("${kubectl[@]}" -n invoiceflow-infra)

password() { "${app[@]}" get secret keycloak-users -o "jsonpath={.data.$1}" | base64 -d; }
admin_password() { "${app[@]}" get secret keycloak-admin -o jsonpath='{.data.password}' | base64 -d; }

pod="$("${app[@]}" get pod -l app.kubernetes.io/name=keycloak -o jsonpath='{.items[0].metadata.name}')"

# Pod root is read-only; point kcadm's config file at the writable /tmp emptyDir.
kcadm() {
  "${app[@]}" exec -i "$pod" -- \
    /opt/keycloak/bin/kcadm.sh --config /tmp/kcadm.config "$@"
}

# Authenticate against the master realm on the local loopback inside the pod
kcadm config credentials --server http://127.0.0.1:8080 --realm master \
  --user admin --password "$(admin_password)" >/dev/null

for user in clerk approver finance; do
  kcadm set-password -r invoiceflow --username "$user" --new-password "$(password "$user")" >/dev/null
  echo "Password set for $user"
done
