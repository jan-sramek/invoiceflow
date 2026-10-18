# InvoiceFlow web

Angular app for InvoiceFlow (Stage 1: list, detail, upload, edit, lifecycle).

## Prerequisites

- Node.js 24.x
- API on `http://localhost:5080` (Development) for `npm start`

## Scripts

| Command | Purpose |
|---|---|
| `npm start` | Dev server; proxies `/api` to the API |
| `npm run build` | Production build |
| `npm test` / `npm run test:ci` | Unit tests (watch / headless once) |
| `npm run lint` | ESLint + Sheriff module boundaries |
| `npm run i18n:check` | English and Czech translation keys must match |
| `npm run api:generate` | Regenerate `src/app/shared/api/schema.d.ts` from `openapi/openapi.json` |

## Structure

See planning ADR-0017: `core/`, `shared/`, `features/invoices/` with `feature-*`, `ui`, `data-access`, `util`.

Until Keycloak (Stage 4), the shell sends `X-Development-User` (`clerk` / `approver` / `finance`) for authenticated writes.
