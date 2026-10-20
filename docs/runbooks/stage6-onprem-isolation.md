# Runbook: Stage 6 on-premises tenant isolation

Exercises NFR-REL-06 / NFR-REL-11 on the local Colima + k3d stack. Use after `make onprem-up`, `make onprem-spike`, and `make provision-tenant`.

The managed development tenant must keep working while the on-prem demo tenant (`slug=onprem`) is broken.

## Metrics

With observability up, look for meter `InvoiceFlow.Tenancy`:

| Metric | Meaning |
| --- | --- |
| `tenant_db.opens` | Successful opens (attribute `tenant.id`) |
| `tenant_db.failures` | Failed opens |
| `tenant_db.breaker_rejections` | Requests rejected while the per-tenant breaker is open |
| `tenant_db.breaker_open` | `1` when that tenant's breaker is open |

Catalog `/health/ready` must stay healthy when only the on-prem DB is down.

## 1. Stop the on-prem server

```bash
make onprem-down
```

Point the API at the on-prem tenant (`Tenancy__DevelopmentTenantId` = `ONPREM_TENANT_ID`, default `b2c24105-dd81-7520-8c75-0ffd4c394a07`) and call list/upload.

**Expect:** `503` with `tenant.unavailable` within about two seconds and `Retry-After`. After a couple of failures, further calls fail immediately (circuit open). Switch back to the development tenant id — list/upload still succeed. Catalog readiness stays ready.

Bring the DB back with `make onprem-up` (reuse existing certs). After the break duration (~30s) the on-prem tenant recovers; re-run `make onprem-spike` if the EndpointSlice was never applied in this cluster.

## 2. Deploy / migrate while on-prem is down

```bash
make onprem-down
make db-migrate
```

**Expect:** Migrator Job completes. Catalog and managed tenants migrate. The on-prem row gets `last_migration_error` set; the Job does not fail the whole run. API calls for the on-prem tenant return `tenant.maintenance` (503). Managed tenants are unaffected.

After `make onprem-up`, run `make db-migrate` again — on-prem migrates, `last_migration_error` clears, traffic resumes.

## 3. Wrong certificate

1. Replace the cluster CA ConfigMap with a different PEM (or empty file), restart API/worker:
   ```bash
   kubectl --context k3d-invoiceflow -n invoiceflow create configmap onprem-postgres-ca \
     --from-literal=ca.crt=not-a-ca --dry-run=client -o yaml | kubectl apply -f -
   kubectl --context k3d-invoiceflow -n invoiceflow rollout restart deployment/api deployment/worker
   ```
2. Call the API as the on-prem tenant.

**Expect:** `tenant.unavailable` (TLS verify-full fails). Managed tenants still work. Restore with `make onprem-spike` or `make provision-tenant` (recreates the ConfigMap from `deploy/onprem/certs/ca.crt`) and restart.

Pass criteria for the spike Job itself: with the real CA, `make onprem-spike` prints `onprem-pong`; with a wrong `PGSSLROOTCERT`, the Job must fail.
