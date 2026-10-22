# k6 load smoke (Stage 8)

Lightweight [k6](https://k6.io/) script for checking that the stack responds under a small concurrent load on a laptop. This is a **smoke / starter** workload; a fuller Stage 8 model (upload, processing, mixed personas) can grow in [`tests/k6/`](../../tests/k6/) later.

## Prerequisites

- App reachable at the ingress (macOS + Colima: `make stack-up` → `http://invoiceflow.localhost:8080`).
- [k6 installed](https://grafana.com/docs/k6/latest/set-up/install-k6/) on your PATH (`k6 version`). The binary is **not** vendored in this repo.

`/health/live` and `/health/ready` stay **inside the cluster** (the API Ingress only publishes `/api`). The smoke script therefore hits:

- `GET /` — Angular shell
- `GET /api/v1/missing` — proves API routing and JSON problem details (`request.not_found`)
- `GET /api/v1/invoices` — expects **401** without a token, or **200** when `K6_ACCESS_TOKEN` is set

No writes (upload, approve, etc.) in this slice.

## Run

From the repo root:

```bash
make k6-smoke
```

Overrides:

```bash
BASE_URL=http://invoiceflow.localhost:8080 make k6-smoke

# Optional: JWT from Keycloak (clerk persona) to include a real list call
export K6_ACCESS_TOKEN="$(…)"   # see credential-rotation-drill.md for token acquisition patterns
make k6-smoke
```

Direct k6 invocation:

```bash
k6 run tests/k6/smoke.js
```

Defaults: **5 VUs**, **30 s** — safe on a developer machine.

## Record results for a portfolio

Keep artefacts **outside git** (or in your portfolio notes). Suggested minimum:

1. Date, git SHA, environment (`make stack-up` vs CI cluster).
2. Command line (`BASE_URL`, whether `K6_ACCESS_TOKEN` was used).
3. k6 summary: `http_req_duration` p95, `http_req_failed`, iterations.
4. One-line interpretation (pass / investigate).

A placeholder template lives in [`k6-load-results.md`](k6-load-results.md).

## Expand later (Stage 8 full workload)

Ideas for a follow-up script (not implemented here):

- Weighted mix: list, detail, document download (read-heavy).
- Separate scenario for upload + wait for extraction (needs auth + fixture PDF).
- Export to Prometheus remote write or Grafana k6 cloud only if the team adopts it — local `k6 run --summary-export=…` is enough for demos.

Related: Playwright smoke ([`tests/e2e/README.md`](../../tests/e2e/README.md)), backup drill ([`backup-restore.md`](backup-restore.md)).
