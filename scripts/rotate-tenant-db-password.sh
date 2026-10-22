#!/usr/bin/env bash
# Rotates one tenant runtime database password: updates the mounted Secret, then PostgreSQL.
# Usage: rotate-tenant-db-password.sh <credentials_key> <postgres_role>
# Example (development tenant drill): rotate-tenant-db-password.sh development tenant_development_app
set -euo pipefail

credentials_key="${1:?credentials_key (Secret file name, e.g. development)}"
role="${2:?postgres login role, e.g. tenant_development_app}"

kubectl=(kubectl --context k3d-invoiceflow)
app=("${kubectl[@]}" -n invoiceflow)
infra=("${kubectl[@]}" -n invoiceflow-infra)

secret=invoiceflow-tenant-databases
new_pw="$(openssl rand -hex 24)"
login="$(jq -nc --arg username "$role" --arg password "$new_pw" '$ARGS.named')"
b64="$(printf '%s' "$login" | base64 | tr -d '\n')"

if "${app[@]}" get secret "$secret" >/dev/null 2>&1; then
  "${app[@]}" patch secret "$secret" --type merge \
    -p "$(jq -nc --arg key "$credentials_key" --arg value "$b64" '{data: {($key): $value}}')" >/dev/null
else
  "${app[@]}" create secret generic "$secret" --from-literal="$credentials_key=$login" >/dev/null
fi

# Password on stdin — not in process arguments
"${infra[@]}" exec -i postgres-0 -- psql -U postgres -v ON_ERROR_STOP=1 -q <<SQL
\set role '$role'
\set password '$new_pw'
ALTER ROLE :"role" PASSWORD :'password';
SQL

echo "Rotated invoiceflow/$secret ($credentials_key) and PostgreSQL role $role."
echo "Restart API/worker so pods reload the Secret and pools drop old connections:"
echo "  kubectl --context k3d-invoiceflow -n invoiceflow rollout restart deployment/api deployment/worker"
