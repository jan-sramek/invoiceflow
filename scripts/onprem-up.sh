#!/usr/bin/env bash
# Starts a TLS-only PostgreSQL on the Docker/Colima host (not inside k3d), port 5433.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
certs="$root/deploy/onprem/certs"
name=invoiceflow-onprem-postgres
image='postgres:18.6-alpine3.24@sha256:77f585114c32fbca283dc835b0596f4e52b51b4c6662d7810b2f4084f60a1873'
port=5433

"$root/scripts/onprem-certs.sh"

chmod 600 "$certs/server.key"

docker rm -f "$name" >/dev/null 2>&1 || true

# Password is local-only; not a production secret.
password_file="$certs/superuser.password"
if [[ ! -f "$password_file" ]]; then
  openssl rand -hex 16 >"$password_file"
  chmod 600 "$password_file"
fi
password="$(cat "$password_file")"

# Alpine postgres runs as uid 70; copy certs into the data dir with matching ownership.
docker run -d --rm --name "$name" \
  -p "127.0.0.1:${port}:5432" \
  -e POSTGRES_PASSWORD="$password" \
  -v "$certs:/certs-ro:ro" \
  "$image" \
  sh -c '
    set -euo pipefail
    mkdir -p /var/lib/postgresql/certs
    cp /certs-ro/server.crt /certs-ro/ca.crt /var/lib/postgresql/certs/
    cp /certs-ro/server.key /var/lib/postgresql/certs/server.key
    chown -R postgres:postgres /var/lib/postgresql/certs
    chmod 600 /var/lib/postgresql/certs/server.key
    exec docker-entrypoint.sh postgres \
      -c ssl=on \
      -c ssl_cert_file=/var/lib/postgresql/certs/server.crt \
      -c ssl_key_file=/var/lib/postgresql/certs/server.key \
      -c ssl_ca_file=/var/lib/postgresql/certs/ca.crt
  ' >/dev/null

for _ in $(seq 1 30); do
  if docker exec "$name" pg_isready -U postgres >/dev/null 2>&1; then
    echo "On-prem PostgreSQL listening on 127.0.0.1:${port} (TLS). Password: $password_file"
    echo "From the cluster use host onprem-postgres.invoiceflow.svc.cluster.local:5432 after make onprem-spike."
    exit 0
  fi
  sleep 1
done

echo "onprem-up: PostgreSQL did not become ready" >&2
docker logs "$name" >&2 || true
exit 1
