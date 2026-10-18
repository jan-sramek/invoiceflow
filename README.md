# InvoiceFlow

Multi-tenant invoice processing with .NET, Angular and Kubernetes. Work in progress.

## Web app

Stage 1 UI lives in [`web/invoiceflow-web`](web/invoiceflow-web).

- Local: from that folder, `npm ci`, `npm start` (proxies `/api` to `http://localhost:5080`).
- Cluster: `make app-up` serves the SPA at `http://invoiceflow.localhost:8080/` and the API at `/api`.
