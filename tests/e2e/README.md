# End-to-end tests

[Playwright](https://playwright.dev) tests that drive the deployed web app in the local k3d cluster.
They run **locally only**; CI does not start a cluster yet (that is Stage 8).

## Smoke test

`tests/smoke.spec.ts` (tagged `@smoke`):

1. opens the app and makes sure it acts as the development user **Clara Clerk** (`clerk`, the default of `DevUserService`);
2. goes to **Upload** and uploads `fixtures/invoice-smoke.pdf`;
3. follows the new invoice's link from the upload result and checks that the same invoice is on the **Invoices** list.

Each run appends a unique comment to the PDF, so repeated runs never end up as duplicates of each other.

## Run it

The app must already be running in the cluster:

```sh
make up          # VM and cluster, if not running yet
make stack-up    # PostgreSQL, SeaweedFS, databases and the app (or just make app-up if the rest is in place)
make e2e-install # once: npm ci and the Chromium browser for Playwright
make e2e-smoke
```

The tests use `http://invoiceflow.localhost:8080` by default. Set `BASE_URL` to test another address:

```sh
BASE_URL=http://localhost:4200 make e2e-smoke
```

On failure Playwright keeps a trace and screenshot in `test-results/`; the HTML report is in `playwright-report/`
(`npm run report` in this folder). Both folders are git-ignored.

`make e2e-typecheck` typechecks the tests without needing a cluster.

## Adding tests

Put specs in `tests/`, fixtures in `fixtures/`. Prefer role- and text-based locators (the tests read the English
texts; the config sets the browser locale to `en-GB`).
