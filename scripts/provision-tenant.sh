#!/usr/bin/env bash
# Provisions the Stage 6 OnPremises demo tenant against the host TLS PostgreSQL.
# Prerequisites: make onprem-up, make onprem-spike, catalog + secrets from make db-up.
# After this script, Makefile runs migrator and restarts API/worker so the CA mount appears.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
certs="$root/deploy/onprem/certs"
kubectl=(kubectl --context k3d-invoiceflow)
app=("${kubectl[@]}" -n invoiceflow)
name=invoiceflow-onprem-postgres

# Fixed id so every rebuild of the local environment gets the same on-prem tenant
tenant_id="${ONPREM_TENANT_ID:-b2c24105-dd81-7520-8c75-0ffd4c394a07}"
db_name=tenant_onprem
migrator_role=tenant_onprem_migrator
app_role=tenant_onprem_app
credentials_key=onprem
host=onprem-postgres.invoiceflow.svc.cluster.local

if ! docker ps --format '{{.Names}}' | grep -qx "$name"; then
  echo "On-prem PostgreSQL is not running (make onprem-up)" >&2
  exit 1
fi

if [[ ! -f "$certs/ca.crt" ]]; then
  echo "Missing $certs/ca.crt (make onprem-up)" >&2
  exit 1
fi

login_password() {
  local secret="$1" key="$2" username="$3" login
  login="$("${app[@]}" get secret "$secret" -o "jsonpath={.data.$key}" 2>/dev/null | base64 -d || true)"

  if [[ -z "$login" ]]; then
    login="$(jq -nc --arg username "$username" --arg password "$(openssl rand -hex 24)" '$ARGS.named')"
    if "${app[@]}" get secret "$secret" >/dev/null 2>&1; then
      "${app[@]}" patch secret "$secret" --type merge \
        -p "$(jq -nc --arg key "$key" --arg value "$(printf '%s' "$login" | base64 | tr -d '\n')" '{data: {($key): $value}}')" >/dev/null
    else
      "${app[@]}" create secret generic "$secret" --from-literal="$key=$login" >/dev/null
    fi
    echo "Login $username stored in invoiceflow/$secret" >&2
  fi

  jq -r .password <<<"$login"
}

migrator_pw="$(login_password invoiceflow-tenant-migrations "$credentials_key" "$migrator_role")"
app_pw="$(login_password invoiceflow-tenant-databases "$credentials_key" "$app_role")"

# Create roles/database on the on-prem server (client-site style).
docker exec -i "$name" psql -U postgres -v ON_ERROR_STOP=1 -q <<SQL
SELECT format('CREATE ROLE %I LOGIN', role)
FROM unnest(ARRAY['$migrator_role', '$app_role']) AS role
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = role) \gexec
ALTER ROLE $migrator_role PASSWORD '$migrator_pw';
ALTER ROLE $app_role PASSWORD '$app_pw';
SELECT 'CREATE DATABASE $db_name OWNER $migrator_role'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$db_name') \gexec
REVOKE ALL ON DATABASE $db_name FROM PUBLIC;
GRANT CONNECT ON DATABASE $db_name TO $app_role;
SQL

# Register in catalog (in-cluster Postgres).
"${kubectl[@]}" -n invoiceflow-infra exec -i postgres-0 -- \
  psql -U postgres -d invoiceflow_catalog -v ON_ERROR_STOP=1 -q \
  -v tenant_id="$tenant_id" \
  -v host="$host" \
  -v db_name="$db_name" \
  -v credentials_key="$credentials_key" \
  -v runtime_role="$app_role" <<'SQL'
INSERT INTO tenants (id, name, slug, status, created_at)
VALUES (:'tenant_id', 'Acme On-Prem', 'onprem', 'Active', now())
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name, slug = EXCLUDED.slug, status = 'Active';

INSERT INTO tenant_databases
  (tenant_id, location, host, port, database_name, ssl_mode, credentials_key, runtime_role)
VALUES
  (:'tenant_id', 'OnPremises', :'host', 5432, :'db_name', 'VerifyFull', :'credentials_key', :'runtime_role')
ON CONFLICT (tenant_id) DO UPDATE SET
  location = EXCLUDED.location,
  host = EXCLUDED.host,
  port = EXCLUDED.port,
  database_name = EXCLUDED.database_name,
  ssl_mode = EXCLUDED.ssl_mode,
  credentials_key = EXCLUDED.credentials_key,
  runtime_role = EXCLUDED.runtime_role,
  last_migration_error = NULL;
SQL

# CA for VerifyFull from API, worker and migrator pods.
"${kubectl[@]}" -n invoiceflow create configmap onprem-postgres-ca \
  --from-file=ca.crt="$certs/ca.crt" --dry-run=client -o yaml | "${kubectl[@]}" apply -f - >/dev/null

echo "OnPremises tenant $tenant_id ($db_name) registered (slug=onprem)."
echo "Next: migrator + API/worker restart (make provision-tenant does both)."
