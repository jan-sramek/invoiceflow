# InvoiceFlow

Multi-tenant invoice processing with .NET, Angular and Kubernetes. Work in progress.

## Web app

Stage 1 UI lives in [`web/invoiceflow-web`](web/invoiceflow-web).

- Local: from that folder, `npm ci`, `npm start` (proxies `/api` to `http://localhost:5080`).
- Cluster: `make stack-up` deploys everything the app needs and serves the SPA at `http://invoiceflow.localhost:8080/` and the API at `/api`. Once the databases exist, `make app-up` redeploys just the app.

## End-to-end tests

Playwright smoke tests live in [`tests/e2e`](tests/e2e) and run locally against the cluster: `make e2e-install`, then `make e2e-smoke` (the app must be up first).
