#!/usr/bin/env bash
# Creates the catalog and the development tenant's database, each with a migration role that owns it
# and a role for the applications, plus the Secrets that hold their logins. Existing passwords are kept.
set -euo pipefail

kubectl=(kubectl --context k3d-invoiceflow)
app=("${kubectl[@]}" -n invoiceflow)

# Usage: login_password <secret> <key> <username>
# Tenant logins are JSON files in a Secret shared by all tenants; missing ones are added with a new password.
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

password() { "${app[@]}" get secret "$1" -o jsonpath='{.data.password}' | base64 -d; }

catalog_migrator="$(password catalog-migrator)"
catalog_app="$(password catalog-app)"
tenant_migrator="$(login_password invoiceflow-tenant-migrations development tenant_development_migrator)"
tenant_app="$(login_password invoiceflow-tenant-databases development tenant_development_app)"

# The script goes in on stdin, so the passwords never show up in process arguments
"${kubectl[@]}" -n invoiceflow-infra exec -i postgres-0 -- psql -U postgres -v ON_ERROR_STOP=1 -q <<SQL
SELECT format('CREATE ROLE %I LOGIN', role)
FROM unnest(ARRAY['catalog_migrator', 'catalog_app', 'tenant_development_migrator', 'tenant_development_app']) AS role
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = role) \gexec
ALTER ROLE catalog_migrator PASSWORD '$catalog_migrator';
ALTER ROLE catalog_app PASSWORD '$catalog_app';
ALTER ROLE tenant_development_migrator PASSWORD '$tenant_migrator';
ALTER ROLE tenant_development_app PASSWORD '$tenant_app';

SELECT 'CREATE DATABASE invoiceflow_catalog OWNER catalog_migrator'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'invoiceflow_catalog') \gexec
SELECT 'CREATE DATABASE tenant_development OWNER tenant_development_migrator'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'tenant_development') \gexec
REVOKE ALL ON DATABASE invoiceflow_catalog FROM PUBLIC;
GRANT CONNECT ON DATABASE invoiceflow_catalog TO catalog_app;
REVOKE ALL ON DATABASE tenant_development FROM PUBLIC;
GRANT CONNECT ON DATABASE tenant_development TO tenant_development_app;

-- Tables the migrator creates later are readable by the applications without another grant.
-- The tenant database gets its grants from the migrator instead, because they differ per table.
\connect invoiceflow_catalog
GRANT USAGE ON SCHEMA public TO catalog_app;
ALTER DEFAULT PRIVILEGES FOR ROLE catalog_migrator IN SCHEMA public GRANT SELECT ON TABLES TO catalog_app;
SQL

echo "Catalog and development tenant databases ready"
