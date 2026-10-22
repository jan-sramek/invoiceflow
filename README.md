# InvoiceFlow

Multi-tenant invoice processing with .NET, Angular and Kubernetes. Work in progress.

## Web app

Stage 1 UI lives in [`web/invoiceflow-web`](web/invoiceflow-web).

- Local (Windows or macOS): from that folder, `npm ci`, `npm start` (proxies `/api` to `http://localhost:5080`). Uses development personas (`X-Development-User`) unless `config.json` sets OIDC.
- Cluster (macOS + Colima + k3d only today): `make stack-up` serves the SPA at `http://invoiceflow.localhost:8080/` and the API at `/api`. Sign in through Keycloak at `https://auth.invoiceflow.localhost:8443`. Windows does not run Colima/`make doctor` yet.

## Authentication (Stage 4)

- Cluster: Keycloak realm `invoiceflow` (`make keycloak-up`), then trust the local CA once with `make trust-local-ca`. Personas: `clerk`, `approver`, `finance` (passwords in secret `keycloak-users`).
- API validates JWT Bearer tokens (`Authentication:Authority`). Development header auth is off in the cluster.
- Local Windows: keep `Authentication:DevelopmentUsers=true` (see `appsettings.Development.json` / user secrets) and the Clara/Adam/Fiona switcher.

## Invoice processing (Stage 5–7)

- Upload and retry write `{ tenantId, invoiceId }` into a tenant **transactional outbox** (`outbox_messages`) in the same DB commit as the invoice change. The API’s `OutboxPublisher` drains unpublished rows to RabbitMQ with publisher confirms (only when `RabbitMq:Host` is set). W3C `traceparent` / `tracestate` are stored on the outbox row and forwarded as RabbitMQ headers so the worker continues the same trace.
- The worker consumes the quorum queue `invoice-processing`. Outcomes are recorded in `processed_messages` (same transaction as the invoice status change) so redelivery cannot double-apply results. Business extraction failures mark **Extraction failed** and ack. Technical failures publish to TTL retry queues (`invoice-processing.retry.10s|1m|5m`) then ack; after those are exhausted the invoice is failed and the message goes to `invoice-processing.dlq`. Poison JSON also goes to the DLQ (not silent drop).
- A **processing sweeper** (worker, when RabbitMQ is configured) re-queues invoices stuck in **Processing** longer than `ProcessingSweeper:StuckAfter` (default 30 minutes) by writing a fresh outbox row, skipping ones that already have an unpublished outbox message.
- On SIGTERM the worker **cancels the consumer first**, waits up to two minutes for in-flight extraction, then exits; any still-unacked message is requeued by RabbitMQ. The Deployment sets `terminationGracePeriodSeconds: 180`.
- Topology is admin-only: `make rabbitmq-up` runs [`scripts/rabbitmq-users.sh`](scripts/rabbitmq-users.sh) and [`scripts/rabbitmq-topology.sh`](scripts/rabbitmq-topology.sh). Applications must not declare exchanges or queues. **Queue arguments are immutable** — if `invoice-processing` already exists as a classic queue, delete it before re-running the topology script (see the script header).
- `make stack-up` includes RabbitMQ. With `DocumentExtraction:ServiceUrl` (Ollama on the host, model `qwen3:0.6b` in the cluster) the worker uses **PdfPig** to read the PDF text layer, then asks Ollama for fields. Extracted values are checked with deterministic validation and **grounding** against the PDF text; each field gets `Valid` / `Missing` / `Invalid` / `Unverified` for the review UI. PDFs without a text layer fail with `extraction.no_text_layer` (scanned/OCR is Stage 9). Without that URL the worker uses a **configurable fake extractor** (`FakeExtractor:Delay`, `FailureRate`, `FailureMode` Technical|Business) so local Windows, tests, and retry drills stay deterministic.
- Ops: [`docs/runbooks/find-failed-invoice.md`](docs/runbooks/find-failed-invoice.md) (trace a failed invoice) and [`docs/runbooks/stage5-failure-experiments.md`](docs/runbooks/stage5-failure-experiments.md) (loss/duplication drills).

## On-premises tenant database (Stage 6)

Simulates a client-site PostgreSQL outside k3d (macOS + Colima, same host-gateway pattern as Ollama). Windows `onprem-up` is out of scope.

1. `make onprem-up` — TLS-only Postgres on the Docker host (`127.0.0.1:5433`), CA/certs under `deploy/onprem/certs/` (gitignored).
2. `make onprem-spike` — selector-less Service + EndpointSlice (`onprem-postgres.invoiceflow` → Colima `192.168.5.2:5433`) and a Job that `psql`s with `sslmode=verify-full`. Pass = cluster DNS + TLS works; wrong CA/host must fail.
3. `make provision-tenant` — registers the Acme On-Prem demo tenant (`slug=onprem`), writes credentials into the tenant Secrets, migrates the on-prem DB, restarts API/worker so the CA ConfigMap is mounted.

Unavailable on-prem DBs fail within ~2s as `tenant.unavailable` (503) with a per-tenant circuit breaker; schema behind / migration error returns `tenant.maintenance`. Catalog readiness stays independent. Isolation drills: [`docs/runbooks/stage6-onprem-isolation.md`](docs/runbooks/stage6-onprem-isolation.md).

## Backups (Stage 8)

Postgres dumps land on the host under `INVOICEFLOW_BACKUP_DIR` (k3d mounts that path at `/var/backups/invoiceflow`). Install with `make backup-up`, run once with `make backup-now`, list with `make backup-list`. Single-tenant restore drill: [`docs/runbooks/backup-restore.md`](docs/runbooks/backup-restore.md).

## Security verification (Stage 8)

- OWASP ASVS 5.0 Level 2 checklist: [`docs/security/asvs-checklist.md`](docs/security/asvs-checklist.md).
- Credential rotation drill (tenant DB password, Keycloak signing key): [`docs/runbooks/credential-rotation-drill.md`](docs/runbooks/credential-rotation-drill.md) (`make rotation-drill`).

## End-to-end tests

Playwright smoke tests live in [`tests/e2e`](tests/e2e). CI runs them on a single-node k3d cluster with the fake extractor (`make e2e-ci` / job **Playwright e2e (k3d)**). On macOS against a local cluster: `make e2e-install`, then `make e2e-smoke`. On Windows against local `ng serve`, see [`tests/e2e/README.md`](tests/e2e/README.md). On-demand axe accessibility scans: `make e2e-a11y` — [`docs/runbooks/accessibility-audit.md`](docs/runbooks/accessibility-audit.md).

k6 ingress smoke (5 VUs, 30s): `make k6-smoke` — [`docs/runbooks/k6-load.md`](docs/runbooks/k6-load.md).

## Architecture note

Manifests live under [`deploy/kubernetes`](deploy/kubernetes) (app, Postgres, SeaweedFS, RabbitMQ, Keycloak, backup CronJob, on-prem spike). `make stack-up` targets a local k3d cluster with Traefik ingress; workloads split across `invoiceflow` and `invoiceflow-infra`. Invoice PDFs go to SeaweedFS; processing is driven by the transactional outbox → RabbitMQ → worker path described above.

## Known limitations

Demo and local-dev constraints only — not a production readiness statement.

- **Cluster tooling:** Full stack (`make doctor`, `make stack-up`, on-prem drills) expects macOS + Colima + k3d. Windows can run the API, Angular, and Playwright against local services; Colima/k3d stack-up is not supported yet ([`tests/e2e/README.md`](tests/e2e/README.md)).
- **k3d storage:** Dev cluster is k3d on Docker; CI uses a single-server k3d layout ([`deploy/k3d/cluster-ci.yaml`](deploy/k3d/cluster-ci.yaml)). Stateful data (Postgres, SeaweedFS, RabbitMQ) lives in cluster volumes — not HA and not portable across machines without your own migration.
- **Backups:** Postgres dumps only, on the host via `INVOICEFLOW_BACKUP_DIR` / k3d mount — no off-cluster copy. SeaweedFS and RabbitMQ are **not** included in the backup CronJob; on-prem tenant Postgres is outside the in-cluster dump. See [`docs/runbooks/backup-restore.md`](docs/runbooks/backup-restore.md).
- **Network policies:** k3d disables the network policy controller (`--disable-network-policy` in [`deploy/k3d/cluster.yaml`](deploy/k3d/cluster.yaml)) so Docker IP churn does not break the cluster. Kubernetes NetworkPolicies in-repo are not enforced on this stack.
- **Extraction:** Without `DocumentExtraction:ServiceUrl`, the worker uses the **fake extractor** (local Windows, tests, CI). Text-layer PDFs only; scans fail with `extraction.no_text_layer` — **OCR / scanned PDFs are Stage 9** ([`docs/error-codes.md`](docs/error-codes.md)).
- **On-prem tenant DB (Stage 6):** Simulates a client Postgres on the Docker/Colima host (EndpointSlice to the host), not a real remote site. `make onprem-up` is out of scope on Windows.
- **Security checklist:** [`docs/security/asvs-checklist.md`](docs/security/asvs-checklist.md) is a manual ASVS 5.0 L2 review artefact, not automated certification.
