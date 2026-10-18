# End-to-end tests

[Playwright](https://playwright.dev) smoke tests that drive the web UI against a running app.
They run **locally only**; CI does not start a cluster yet (that is Stage 8).

## Environments

| Where you work | How the app is served | Smoke target |
|---|---|---|
| **macOS + Colima** (project default) | `make up` then `make stack-up` → Traefik at `http://invoiceflow.localhost:8080` | `make e2e-smoke` |
| **Windows** | No Colima/`make stack-up` yet. Run API + Angular locally (see below), or use a remote Mac/CI later. | PowerShell + `BASE_URL` |

`make doctor` / `make stack-up` expect Colima (`DOCKER_HOST=unix://…/.colima/invoiceflow/docker.sock`) and k3d. That path is not supported on Windows today.

## Smoke test

`tests/smoke.spec.ts` (tagged `@smoke`):

1. opens the app and makes sure it acts as the development user **Clara Clerk** (`clerk`, the default of `DevUserService`);
2. goes to **Upload** and uploads `fixtures/invoice-smoke.pdf`;
3. follows the new invoice's link from the upload result and checks that the same invoice is on the **Invoices** list.

Each run appends a unique comment to the PDF, so repeated runs never end up as duplicates of each other.

## Run it (macOS / Colima)

```sh
make up          # VM and cluster, if not running yet
make stack-up    # PostgreSQL, SeaweedFS, databases and the app (or just make app-up if the rest is in place)
make e2e-install # once: npm ci and the Chromium browser for Playwright
make e2e-smoke
```

## Run it (Windows, local API + `ng serve`)

You need a Development API with catalog/tenant databases (and optional document storage). The Angular app proxies `/api` to `http://localhost:5080`.

```powershell
# once
cd tests/e2e
npm ci
npx playwright install chromium

# terminal 1 — API (from repo root; configure ConnectionStrings as you do for local Development)
dotnet run --project src/InvoiceFlow.Api --launch-profile http

# terminal 2 — web
cd web/invoiceflow-web
npm start

# terminal 3 — smoke
cd tests/e2e
$env:BASE_URL = "http://localhost:4200"
npm run test:smoke
```

Without a working catalog/tenant database, upload will fail even if the UI loads.

## Overrides

Default `BASE_URL` is `http://invoiceflow.localhost:8080`.

```sh
# bash / make
BASE_URL=http://localhost:4200 make e2e-smoke
```

```powershell
$env:BASE_URL = "http://localhost:4200"
npm run test:smoke --prefix tests/e2e
```

On failure Playwright keeps a trace and screenshot in `test-results/`; the HTML report is in `playwright-report/`
(`npm run report` in this folder). Both folders are git-ignored.

`make e2e-typecheck` / `npm run typecheck` typecheck without needing an app.

## Adding tests

Put specs in `tests/`, fixtures in `fixtures/`. Prefer role- and text-based locators (the tests read the English
texts; the config sets the browser locale to `en-GB`).
