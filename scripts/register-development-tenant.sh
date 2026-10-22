#!/usr/bin/env bash
# Registers the development tenant in the catalog. Needs the catalog schema, so it runs after the
# first migration; the next migration then sets up the tenant's database.
# Usage: register-development-tenant.sh <tenant id>
set -euo pipefail

tenant_id="$1"

kubectl --context k3d-invoiceflow -n invoiceflow-infra exec -i postgres-0 -- \
  psql -U postgres -d invoiceflow_catalog -v ON_ERROR_STOP=1 -q -v tenant_id="$tenant_id" <<'SQL'
INSERT INTO tenants (id, name, slug, status, created_at)
VALUES (:'tenant_id', 'Development', 'development', 'Active', now())
ON CONFLICT (id) DO NOTHING;

INSERT INTO tenant_databases
  (tenant_id, location, host, port, database_name, ssl_mode, credentials_key, runtime_role)
VALUES
  (:'tenant_id', 'Managed', 'postgres-0.postgres-headless.invoiceflow-infra.svc.cluster.local', 5432, 'tenant_development',
   'Disable', 'development', 'tenant_development_app')
ON CONFLICT (tenant_id) DO NOTHING;
SQL

echo "Development tenant $tenant_id registered"
