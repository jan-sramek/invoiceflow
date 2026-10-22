# Postgres backup and single-tenant restore drill

Stage 8: backups land on the host under `INVOICEFLOW_BACKUP_DIR` (default `~/.local/share/invoiceflow/backups`), mounted into every k3d node at `/var/backups/invoiceflow`. The CronJob `postgres-backup` in `invoiceflow-infra` writes a dated folder with:

- `all.sql.gz` — full `pg_dumpall` (globals + every database)
- `<database>.dump` — custom-format dump per database (for a single-tenant restore)
- `latest` — symlink to the most recent successful run

Retention is seven days (directories older than that are removed after each successful run).

## Install / trigger

```bash
make backup-up          # apply the CronJob (idempotent)
make backup-now         # run one Job immediately
make backup-list        # list epochs on the host
```

Host path: `$INVOICEFLOW_BACKUP_DIR/postgres/<UTC-stamp>/`.

## Single-tenant restore drill (timed against RTO 1 h)

Goal: restore **one** tenant database from the latest backup into a throwaway database, verify row counts, then drop it. Do not overwrite the live tenant DB during the drill.

Preconditions: `make stack-up`, at least one successful `make backup-now`, kubectl context is the local invoiceflow cluster.

1. Note the start time.

2. Pick the tenant database name (development tenant is `tenant_development`):

   ```bash
   stamp=$(readlink "$INVOICEFLOW_BACKUP_DIR/postgres/latest")
   dump="$INVOICEFLOW_BACKUP_DIR/postgres/$stamp/tenant_development.dump"
   test -f "$dump"
   ```

3. Copy the dump into the Postgres pod and restore into a new database:

   ```bash
   kubectl -n invoiceflow-infra cp "$dump" postgres-0:/tmp/tenant_development.dump
   kubectl -n invoiceflow-infra exec postgres-0 -- \
     psql -U postgres -v ON_ERROR_STOP=1 \
     -c "DROP DATABASE IF EXISTS tenant_development_restore_drill WITH (FORCE);" \
     -c "CREATE DATABASE tenant_development_restore_drill;"
   kubectl -n invoiceflow-infra exec postgres-0 -- \
     pg_restore -U postgres --no-owner --dbname=tenant_development_restore_drill \
     /tmp/tenant_development.dump
   ```

4. Spot-check (adjust table names if the schema changes):

   ```bash
   kubectl -n invoiceflow-infra exec postgres-0 -- \
     psql -U postgres -d tenant_development_restore_drill -c "SELECT count(*) FROM invoices;"
   ```

5. Clean up:

   ```bash
   kubectl -n invoiceflow-infra exec postgres-0 -- \
     psql -U postgres -c "DROP DATABASE tenant_development_restore_drill WITH (FORCE);"
   kubectl -n invoiceflow-infra exec postgres-0 -- rm -f /tmp/tenant_development.dump
   ```

6. Record elapsed time. Pass when restore + spot-check finish in under 60 minutes on this laptop/cluster.

## Full cluster restore (disaster only)

Stop API/worker, then:

```bash
stamp=$(readlink "$INVOICEFLOW_BACKUP_DIR/postgres/latest")
gunzip -c "$INVOICEFLOW_BACKUP_DIR/postgres/$stamp/all.sql.gz" \
  | kubectl -n invoiceflow-infra exec -i postgres-0 -- psql -U postgres -v ON_ERROR_STOP=1
```

This drops and recreates databases (`pg_dumpall --clean`). Use only when you intend to replace the whole cluster state.

## Known limits

- Backups are local hostPath only (demo). There is no off-cluster copy.
- SeaweedFS object store and RabbitMQ are not covered by this CronJob.
- On-premises tenant Postgres (Stage 6) is outside this dump; back it up with the customer's tooling.
