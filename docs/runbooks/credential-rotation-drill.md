# Credential rotation drill

Stage 8 exercise for **NFR-SEC-19**: rotate credentials while the stack stays up and users can keep working (apart from in-flight retries and token lifetime).

This runbook covers the two drills called out in the roadmap:

1. **Managed tenant database password** — development tenant runtime role `tenant_development_app` (Secret `invoiceflow/invoiceflow-tenant-databases`, file key `development`).
2. **Keycloak realm signing key** — new RS256 key for realm `invoiceflow`; the API validates JWTs via OIDC discovery and JWKS (`Authentication:Authority`).

Other local secrets (Postgres superuser, RabbitMQ, SeaweedFS, Keycloak DB/admin, persona passwords) are created once by [`scripts/create-local-secrets.sh`](../../scripts/create-local-secrets.sh) and are **not** rotated here. Production would extend the same pattern to RabbitMQ users ([`scripts/rabbitmq-users.sh`](../../scripts/rabbitmq-users.sh)) and storage keys.

## Preconditions

- `make stack-up` (or at least Postgres, Keycloak, API, worker).
- kubectl context `k3d-invoiceflow`.
- For browser checks: `make trust-local-ca` once, then sign in at `https://auth.invoiceflow.localhost:8443`.
- Record start time, operator, and pass/fail in your notes; this file is the procedure, not the evidence log.

## How secrets are wired (local cluster)

| Credential | Kubernetes Secret | Notes |
| --- | --- | --- |
| Infra DB passwords | `invoiceflow-infra/postgres-superuser`, `keycloak-db`, … | Random on first `create-local-secrets.sh`; unchanged on re-run |
| Tenant runtime logins | `invoiceflow/invoiceflow-tenant-databases` | One JSON file per `credentials_key` (`username` + `password`); mounted at `/var/run/secrets/invoiceflow/tenant-databases` on API/worker |
| Tenant migrator logins | `invoiceflow/invoiceflow-tenant-migrations` | Same shape; used by the migrator Job |
| Keycloak personas | `invoiceflow-infra/keycloak-users` | Applied with [`scripts/keycloak-users.sh`](../../scripts/keycloak-users.sh) after realm import |
| Keycloak admin bootstrap | `invoiceflow-infra/keycloak-admin` | `make keycloak-password`; admin UI via `make keycloak-admin` only (not on ingress) |
| Realm OIDC keys | Keycloak DB | Generated at runtime; realm JSON import does not ship private keys |

Initial tenant passwords are provisioned in [`scripts/app-databases.sh`](../../scripts/app-databases.sh) (managed development DB) or [`scripts/provision-tenant.sh`](../../scripts/provision-tenant.sh) (on-prem demo).

---

## Drill A — tenant database password (timed)

**Goal:** Change `tenant_development_app` password with no prolonged outage for the development tenant.

1. While signed in (or with a valid access token), call a tenant-scoped endpoint — e.g. list invoices in the UI or `GET /api/invoices` through the ingress.

2. Rotate credentials (updates the Secret and PostgreSQL):

   ```bash
   ./scripts/rotate-tenant-db-password.sh development tenant_development_app
   ```

3. Restart API and worker so pods reload the Secret and connection pools are cleared:

   ```bash
   kubectl --context k3d-invoiceflow -n invoiceflow rollout restart deployment/api deployment/worker
   kubectl --context k3d-invoiceflow -n invoiceflow rollout status deployment/api --timeout=3m
   kubectl --context k3d-invoiceflow -n invoiceflow rollout status deployment/worker --timeout=3m
   ```

   (The resolver caches catalog credentials for about one minute, but pools may keep old passwords until restart.)

4. Repeat the same API/UI action. **Expect:** success without re-login (JWT unchanged).

5. Optional negative check — old password must fail:

   ```bash
   kubectl --context k3d-invoiceflow -n invoiceflow-infra exec postgres-0 -- \
     psql -U postgres -d tenant_development -c "SELECT 1" \
     --username=tenant_development_app
   ```

   Use the previous password if you saved it; connection must be rejected.

6. Record elapsed time from step 2 through successful step 4. **Pass:** under a few minutes on the local cluster, no data loss, catalog and other tenants untouched.

---

## Drill B — Keycloak signing key (timed)

**Goal:** Add a new realm signing key; new logins use it; the API still accepts tokens until old keys are removed from JWKS.

Realm settings (imported): access token lifespan **300 s** ([`deploy/keycloak/invoiceflow-realm.json`](../../deploy/keycloak/invoiceflow-realm.json)).

1. Capture current JWKS key count:

   ```bash
   curl -sk "https://auth.invoiceflow.localhost:8443/realms/invoiceflow/protocol/openid-connect/certs" \
     | jq '.keys | length'
   ```

2. With an existing session, keep using the app (list invoices).

3. Add a new RSA key (same pattern as `make keycloak-up` → `keycloak-users.sh`, via `kcadm` inside the pod):

   ```bash
   ./scripts/keycloak-rotate-signing-key.sh
   ```

4. JWKS should expose **one more** key:

   ```bash
   curl -sk "https://auth.invoiceflow.localhost:8443/realms/invoiceflow/protocol/openid-connect/certs" \
     | jq '.keys | length'
   ```

5. **Existing access token:** step 2 should still work until the token expires (~5 minutes).

6. Sign out and sign in again (or refresh via OIDC). Use the app — **expect** success; new tokens are signed with the new key.

7. **Pass:** no manual API config change; only failed requests are expired tokens if you wait out the lifespan without re-login.

Removing retired keys from the realm is a separate, deliberate step in production (wait until all issued tokens expire). This drill leaves old keys in JWKS on purpose.

---

## Makefile shortcuts

```bash
make rotation-drill   # print this runbook path
```

---

## Related docs

- ASVS Level 2 tracking: [`docs/security/asvs-checklist.md`](../security/asvs-checklist.md)
- Backup / restore (Stage 8): [`backup-restore.md`](backup-restore.md)
