# End-to-end tests

[Playwright](https://playwright.dev) smoke tests that drive the web UI against a running app.
Locally they target macOS + Colima + k3d (or Windows local API + `ng serve`).
In CI, the **Playwright e2e (k3d)** job creates a single-node k3d cluster, deploys the stack with the **fake extractor** (no Ollama), and runs `@smoke`.

## Environments

| Where you work | How the app is served | Smoke target |
|---|---|---|
| **GitHub Actions** | `make e2e-ci` (k3d from [`deploy/k3d/cluster-ci.yaml`](../../deploy/k3d/cluster-ci.yaml)) | CI job `Playwright e2e (k3d)` |
| **macOS + Colima** (project default) | `make up` then `make stack-up` → Traefik at `http://invoiceflow.localhost:8080` | `make e2e-smoke` |
| **Windows** | No Colima/`make stack-up` yet. Run API + Angular locally (see below), or use CI. | PowerShell + `BASE_URL` |

`make doctor` / `make stack-up` expect Colima (`DOCKER_HOST=unix://…/.colima/invoiceflow/docker.sock`) and k3d on a developer Mac. That path is not supported on Windows today. CI sets `CI=true` so doctor accepts the runner Docker engine instead.

## Smoke test

`tests/smoke.spec.ts` (tagged `@smoke`):

1. opens the app and ensures the signed-in user is **Clara Clerk** (Keycloak on the cluster, or the development switcher on Windows);
2. goes to **Upload** and uploads `fixtures/invoice-smoke.pdf`;
3. follows the new invoice's link from the upload result and checks that the same invoice is on the **Invoices** list.

Each run appends a unique comment to the PDF, so repeated runs never end up as duplicates of each other.

## Workflow test

`tests/workflow.spec.ts` (tagged `@workflow`) extends smoke with a multi-persona path:

1. **Clara Clerk** uploads a unique PDF and opens the new invoice;
2. waits for **Needs review** (fake extractor finished — see below);
3. fills required fields, saves, and **Submits for approval**;
4. **Adam Approver** approves;
5. **Fiona Finance** marks the invoice **Paid**;
6. asserts **Paid** on the list and detail.

CI does **not** run this test (`make e2e-ci` keeps `@smoke` only). Run it on demand like accessibility:

```sh
make e2e-workflow   # sets E2E_CLERK_PASSWORD, E2E_APPROVER_PASSWORD, E2E_FINANCE_PASSWORD from keycloak-users
```

On Windows local dev (`ng serve`), set the three `E2E_*_PASSWORD` variables only when using Keycloak; otherwise the development **Acting as** switcher is used (personas switch without OIDC sign-out).

### Fake extractor and statuses

When `DocumentExtraction:ServiceUrl` is empty (CI, `make e2e-ci`, default local stack without Ollama), the worker uses **FakeInvoiceExtractor**:

| Step | Invoice status | Notes |
|------|----------------|--------|
| Upload completes | `Uploaded` | API returns; upload queue shows “Uploaded”. |
| Worker picks message | `Processing` | `StartProcessing`. |
| Fake extract succeeds (default: no delay, no failures) | `NeedsReview` | `RequireReview` then `ApplyExtraction` with **empty** fields. |
| Clerk saves fields + submit | `PendingApproval` | Submit requires supplier, invoice number, date, currency, total. |
| Approver approves | `Approved` | Approver must differ from submitter. |
| Finance marks paid | `Paid` | Payment dialog (date + reference). |

Configured failures (`FakeExtractor:FailureRate` / `Delay`) can leave invoices in `Processing`, `ExtractionFailed`, or retry loops — not used in CI defaults.

**Flakiness:** extraction is async (RabbitMQ + worker). The workflow test polls the detail page for up to 90s per status transition. Slow clusters or elevated fake delay may need a longer Playwright timeout (the spec uses 180s).

## Run it (macOS / Colima)

```sh
make up          # VM and cluster, if not running yet
make stack-up    # PostgreSQL, SeaweedFS, Keycloak, RabbitMQ, databases and the app
make trust-local-ca  # once: trust the auth.invoiceflow.localhost certificate
make e2e-install # once: npm ci and the Chromium browser for Playwright
make e2e-smoke
```

Sign in as `clerk` when Keycloak asks (password from `kubectl -n invoiceflow-infra get secret keycloak-users -o jsonpath='{.data.clerk}' | base64 -d`).

## Run it (CI locally)

Same entrypoint the workflow uses (requires Docker, k3d, kubectl, helm, Node 24):

```sh
export CI=true
export KUBECONFIG="$PWD/.kube/config"
export INVOICEFLOW_BACKUP_DIR="${INVOICEFLOW_BACKUP_DIR:-$HOME/.local/share/invoiceflow/backups}"
# CI defaults: PUSH_REGISTRY=127.0.0.1:5001 (IPv4 push), REGISTRY=localhost:5001 (cluster pull via k3d mirror)
make e2e-ci
```

This creates the CI cluster config, deploys with `DOCUMENT_EXTRACTION_SERVICE_URL` empty (fake extractor), then runs smoke. Playwright sets `ignoreHTTPSErrors` when `CI` is set so the local Keycloak CA does not block the browser.

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

# optional full workflow (dev persona switcher; no Keycloak passwords needed)
npm run test:workflow
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
(`npm run report` in this folder). Both folders are git-ignored. CI uploads them as the `playwright-report` artifact when the job fails.

`make e2e-typecheck` / `npm run typecheck` typecheck without needing an app.

## Adding tests

Put specs in `tests/`, fixtures in `fixtures/`. Prefer role- and text-based locators (the tests read the English
texts; the config sets the browser locale to `en-GB`).
