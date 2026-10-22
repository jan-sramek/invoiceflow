# InvoiceFlow web

Angular app for InvoiceFlow (Stage 1: list, detail, upload, edit, lifecycle).

## Prerequisites

- Node.js 24.x
- API on `http://localhost:5080` (Development) for `npm start`

## Cluster

`make app-up` builds this image, deploys it behind Traefik at `http://invoiceflow.localhost:8080/`, and keeps `/api` on the API. Runtime `config.json` uses relative `/api`, so one build works locally and in k3d.

## Scripts

| Command | Purpose |
|---|---|
| `npm start` | Dev server; proxies `/api` to the API |
| `npm run build` | Production build |
| `npm test` / `npm run test:ci` | Unit tests (watch / headless once) |
| `npm run lint` | ESLint + Sheriff module boundaries |
| `npm run i18n:check` | English, Czech and German translation keys must match |
| `npm run api:generate` | Regenerate `src/app/shared/api/schema.d.ts` from `openapi/openapi.json` |

## Structure

See planning ADR-0017: `core/`, `shared/`, `features/invoices/` with `feature-*`, `ui`, `data-access`, `util`.

In the cluster, sign in with Keycloak (OIDC in `config.json`). Locally, `public/config.json` keeps `developmentUsers: true` so the shell sends `X-Development-User` (`clerk` / `approver` / `finance`).
