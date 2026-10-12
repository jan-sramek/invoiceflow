#!/usr/bin/env bash
# Creates a database owned by a login role of the same name, if they don't exist yet.
# Usage: postgres-database.sh <name> <secret in invoiceflow-infra holding the role's password>
set -euo pipefail

name="$1"
secret="$2"
kubectl=(kubectl --context k3d-invoiceflow -n invoiceflow-infra)
password="$("${kubectl[@]}" get secret "$secret" -o jsonpath='{.data.password}' | base64 -d)"

# The script goes in on stdin, so the password never shows up in process arguments
"${kubectl[@]}" exec -i postgres-0 -- psql -U postgres -v ON_ERROR_STOP=1 -q <<SQL
\set name '$name'
\set password '$password'
SELECT format('CREATE ROLE %I LOGIN', :'name')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'name') \gexec
ALTER ROLE :"name" PASSWORD :'password';
SELECT format('CREATE DATABASE %I OWNER %I', :'name', :'name')
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = :'name') \gexec
REVOKE ALL ON DATABASE :"name" FROM PUBLIC;
SQL

echo "Database $name ready"
