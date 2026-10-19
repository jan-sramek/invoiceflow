# InvoiceFlow

Multi-tenant invoice processing with .NET, Angular and Kubernetes. Work in progress.

## Web app

Stage 1 UI lives in [`web/invoiceflow-web`](web/invoiceflow-web).

- Local (Windows or macOS): from that folder, `npm ci`, `npm start` (proxies `/api` to `http://localhost:5080`). Uses development personas (`X-Development-User`) unless `config.json` sets OIDC.
- Cluster (macOS + Colima + k3d only today): `make stack-up` serves the SPA at `http://invoiceflow.localhost:8080/` and the API at `/api`. Sign in through Keycloak at `https://auth.invoiceflow.localhost:8080`. Windows does not run Colima/`make doctor` yet.

## Authentication (Stage 4)

- Cluster: Keycloak realm `invoiceflow` (`make keycloak-up`), then trust the local CA once with `make trust-local-ca`. Personas: `clerk`, `approver`, `finance` (passwords in secret `keycloak-users`).
- API validates JWT Bearer tokens (`Authentication:Authority`). Development header auth is off in the cluster.
- Local Windows: keep `Authentication:DevelopmentUsers=true` (see `appsettings.Development.json` / user secrets) and the Clara/Adam/Fiona switcher.

## Invoice processing (Stage 5)

- After a successful upload commit, the API publishes `{ tenantId, invoiceId }` to the durable topic exchange `invoiceflow.events` with routing key `invoice.uploaded`.
- The worker consumes the durable queue `invoice-processing`, sets the tenant from the message, runs extraction, then moves the invoice to **Needs review** (or **Extraction failed** on hard errors / exhausted retries).
- Topology is admin-only: `make rabbitmq-up` runs [`scripts/rabbitmq-users.sh`](scripts/rabbitmq-users.sh) and [`scripts/rabbitmq-topology.sh`](scripts/rabbitmq-topology.sh). Applications must not declare exchanges or queues.
- `make stack-up` includes RabbitMQ. Optional extraction uses Ollama on the host (`DocumentExtraction:ServiceUrl`, model `qwen3:0.6b` in the cluster). Without that URL the worker uses a stub extractor so local Windows and tests stay usable.

## End-to-end tests

Playwright smoke tests live in [`tests/e2e`](tests/e2e). On macOS against k3d: `make e2e-install`, then `make e2e-smoke` (sign in as Clara Clerk when prompted). On Windows against local `ng serve`, see [`tests/e2e/README.md`](tests/e2e/README.md).
