# Accessibility audit (Stage 8 foundation)

Automated checks use [axe-core](https://github.com/dequelabs/axe-core) via Playwright (`@axe-core/playwright`). Specs live in [`tests/e2e/tests/a11y.spec.ts`](../../tests/e2e/tests/a11y.spec.ts), tagged `@a11y`. They are **not** run by `make e2e-ci` or the default smoke grep — run them on demand to keep CI fast.

Each test signs in as **Clara Clerk**, opens a clerk-facing route, runs a full-page scan, and **fails on `serious` or `critical` impact** violations. Moderate/minor findings are reported in the axe output but do not fail the run (triage them in the manual pass below).

## Preconditions

Same as Playwright smoke: a running app at `BASE_URL` (default `http://invoiceflow.localhost:8080`). See [`tests/e2e/README.md`](../../tests/e2e/README.md) for macOS cluster, CI cluster, or Windows local API + `ng serve`.

- **Cluster / Keycloak:** `E2E_CLERK_PASSWORD` must be set ( `make e2e-a11y` reads it from the `keycloak-users` secret when kubectl works).
- **Windows development:** omit `E2E_CLERK_PASSWORD`; use the persona switcher.

One-time browser install: `make e2e-install` or `npm ci` + `npx playwright install chromium` under `tests/e2e`.

## Run automated audit

```bash
make e2e-install   # if needed
make stack-up      # macOS cluster, or point BASE_URL at local ng serve
make e2e-a11y
```

```powershell
cd tests/e2e
npm ci
npx playwright install chromium
$env:BASE_URL = "http://localhost:4200"
npm run test:a11y
```

On failure, open the HTML report: `npm run report --prefix tests/e2e`. Traces and screenshots are under `tests/e2e/test-results/` (git-ignored).

## Record results

Keep a short log for each audit (release gate, quarterly review, or post-UI change):

| Field | Example |
| --- | --- |
| Date (UTC) | 2026-10-22 |
| Commit / branch | `abc1234` on `main` |
| Environment | k3d ingress / local `ng serve` |
| Command | `make e2e-a11y` |
| Automated outcome | pass / fail (paste violation summary from Playwright) |
| Manual spot-check | keyboard nav main nav + upload; focus visible; form labels |
| Follow-ups | issue links or “none” |

Store completed rows in your team’s usual place (release notes, ticket, or `docs/runbooks/` append-only section in a PR). This repo does not commit audit logs by default.

## Manual supplement (recommended)

Automated axe scans do not replace manual testing. For each audit, briefly verify:

1. **Keyboard:** Tab through main navigation, open Upload, activate primary controls without a mouse.
2. **Focus:** Visible focus ring on interactive elements.
3. **Zoom:** 200% browser zoom; no loss of content or horizontal scroll on list/upload.
4. **Screen reader (optional):** Landmarks (`main`, `nav`), headings, and button/link names match visible UI (English, `en-GB` locale in tests).

Note any gaps in the results table even when `@a11y` passes.

## Expanding coverage

Add new `@a11y` tests under `tests/e2e/tests/` for other routes (invoice detail, review) using the same `ensureClaraClerk` / `expectNoSeriousOrCriticalViolations` pattern. Keep new specs out of `@smoke` unless product asks for CI gating.
