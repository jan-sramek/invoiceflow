# InvoiceFlow

Multi-tenant invoice processing with .NET, Angular and Kubernetes. Work in progress.

## Web app

Stage 1 UI lives in [`web/invoiceflow-web`](web/invoiceflow-web).

- Local (Windows or macOS): from that folder, `npm ci`, `npm start` (proxies `/api` to `http://localhost:5080`).
- Cluster (macOS + Colima + k3d only today): `make stack-up` serves the SPA at `http://invoiceflow.localhost:8080/` and the API at `/api`. Windows does not run Colima/`make doctor` yet.

## End-to-end tests

Playwright smoke tests live in [`tests/e2e`](tests/e2e). On macOS against k3d: `make e2e-install`, then `make e2e-smoke`. On Windows against local `ng serve`, see [`tests/e2e/README.md`](tests/e2e/README.md).