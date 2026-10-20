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

## Invoice processing (Stage 5–7)

- Upload and retry write `{ tenantId, invoiceId }` into a tenant **transactional outbox** (`outbox_messages`) in the same DB commit as the invoice change. The API’s `OutboxPublisher` drains unpublished rows to RabbitMQ with publisher confirms (only when `RabbitMq:Host` is set).
- The worker consumes the quorum queue `invoice-processing`. Outcomes are recorded in `processed_messages` (same transaction as the invoice status change) so redelivery cannot double-apply results. Business extraction failures mark **Extraction failed** and ack. Technical failures publish to TTL retry queues (`invoice-processing.retry.10s|1m|5m`) then ack; after those are exhausted the invoice is failed and the message goes to `invoice-processing.dlq`. Poison JSON also goes to the DLQ (not silent drop).
- A **processing sweeper** (worker, when RabbitMQ is configured) re-queues invoices stuck in **Processing** longer than `ProcessingSweeper:StuckAfter` (default 30 minutes) by writing a fresh outbox row, skipping ones that already have an unpublished outbox message.
- On SIGTERM the worker **cancels the consumer first**, waits up to two minutes for in-flight extraction, then exits; any still-unacked message is requeued by RabbitMQ. The Deployment sets `terminationGracePeriodSeconds: 180`.
- Topology is admin-only: `make rabbitmq-up` runs [`scripts/rabbitmq-users.sh`](scripts/rabbitmq-users.sh) and [`scripts/rabbitmq-topology.sh`](scripts/rabbitmq-topology.sh). Applications must not declare exchanges or queues. **Queue arguments are immutable** — if `invoice-processing` already exists as a classic queue, delete it before re-running the topology script (see the script header).
- `make stack-up` includes RabbitMQ. With `DocumentExtraction:ServiceUrl` (Ollama on the host, model `qwen3:0.6b` in the cluster) the worker uses **PdfPig** to read the PDF text layer, then asks Ollama for fields. PDFs without a text layer fail with `extraction.no_text_layer` (scanned/OCR is Stage 9). Without that URL the worker uses a **configurable fake extractor** (`FakeExtractor:Delay`, `FailureRate`, `FailureMode` Technical|Business) so local Windows, tests, and retry drills stay deterministic.

## End-to-end tests

Playwright smoke tests live in [`tests/e2e`](tests/e2e). On macOS against k3d: `make e2e-install`, then `make e2e-smoke` (sign in as Clara Clerk when prompted). On Windows against local `ng serve`, see [`tests/e2e/README.md`](tests/e2e/README.md).
