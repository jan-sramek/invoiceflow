# Runbook: find why invoice X failed

**NFR-OBS-02.** Given an invoice id (and tenant), an operator should reconstruct API → outbox → RabbitMQ → worker → extraction within about five minutes. Never open the PDF or log document text.

## Inputs

- Tenant id (or slug / demo tenant name)
- Invoice id (GUID)
- Approximate failure time (optional)

## 1. Read the invoice (UI or API)

1. Open `http://invoiceflow.localhost:8080/` (cluster) or the local SPA.
2. Open the invoice detail page.
3. Note:
   - **Status** (`ExtractionFailed`, `Processing`, `NeedsReview`, …)
   - **Extraction failure reason** (catalogue code such as `extraction.no_text_layer` or a short technical message)
   - **Audit** tab: `invoice.processing_started`, `invoice.extraction_failed`, `invoice.extraction_retried`, …
4. Copy the **trace id** if the UI or API error body exposed one on the upload/retry request; otherwise continue from logs.

Useful API (as a clerk/approver):

```bash
kubectl --context k3d-invoiceflow -n invoiceflow exec -it deploy/api -- \
  curl -sS -H "Authorization: Bearer $TOKEN" \
  "http://localhost:8080/api/v1/invoices/<invoice-id>"
```

On local Windows without the cluster, call `http://localhost:5080` with `X-Development-User: clerk`.

## 2. Follow the trace (preferred)

Traces span upload → outbox publish → worker consume (`InvoiceFlow.Messaging`).

1. Open Grafana: `http://grafana.invoiceflow.localhost:8080`
2. Explore → Tempo (or use the Aspire dashboard: `http://dashboard.invoiceflow.localhost:8080`)
3. Search by **trace id**, or by attribute `invoice.id` / `tenant.id` if indexed.
4. Confirm the span chain:
   - API use case / HTTP request
   - Outbox publish (same `traceparent` stored on `outbox_messages.headers`)
   - Worker `invoice.uploaded` consumer
   - HTTP client call to Ollama (when `DocumentExtraction:ServiceUrl` is set)

If the worker span is missing, the message may still be unpublished, sitting in a retry queue, or on the DLQ (steps 3–4).

## 3. Tenant database: outbox and processed messages

Connect with the tenant runtime credentials (secret `invoiceflow-tenant-databases`, file named by credentials key) or a migrator/admin path you already use.

```sql
-- Was the event written with the invoice?
SELECT id, type, created_at, published_at, attempts, headers
FROM outbox_messages
WHERE payload->>'invoiceId' = '<invoice-id>'
ORDER BY created_at;

-- Did a worker delivery already commit an outcome for that message id?
SELECT message_id, processed_at
FROM processed_messages
WHERE message_id IN (
  SELECT id FROM outbox_messages WHERE payload->>'invoiceId' = '<invoice-id>'
);
```

Interpretation:

| Observation | Likely cause |
| --- | --- |
| No outbox row | Upload/retry never committed, or wrong tenant database |
| `published_at` null, growing `attempts` | RabbitMQ down / API outbox drain failing |
| Published, no `processed_messages` | Still in flight, in a TTL retry queue, or stuck in `Processing` (sweeper should re-queue after `ProcessingSweeper:StuckAfter`) |
| `processed_messages` present + status `ExtractionFailed` | Worker finished; use audit reason / logs for business vs exhausted technical retries |

## 4. RabbitMQ queues

```bash
kubectl --context k3d-invoiceflow -n invoiceflow-infra exec -i rabbitmq-0 -- \
  rabbitmqctl list_queues -p invoiceflow name messages consumers
```

Check:

- `invoice-processing` — waiting for a worker
- `invoice-processing.retry.10s` / `.1m` / `.5m` — technical backoff
- `invoice-processing.dlq` — poison JSON or retries exhausted

Peek DLQ (ops only; payload is `{ tenantId, invoiceId }`, not the PDF):

```bash
kubectl --context k3d-invoiceflow -n invoiceflow-infra exec -i rabbitmq-0 -- \
  rabbitmqctl list_queues -p invoiceflow name messages
```

If the invoice is on the DLQ and status is already `ExtractionFailed`, the user can **Retry extraction** from the UI (new outbox message). Replaying DLQ messages manually is out of scope for Stage 5.

## 5. Worker and API logs (Loki)

Grafana → Explore → Loki. Example LogQL (adjust labels to your scrape config):

```logql
{namespace="invoiceflow", app="worker"} |= "<invoice-id>"
```

```logql
{namespace="invoiceflow", app="api"} |= "<invoice-id>"
```

Or with kubectl if Loki is unavailable:

```bash
kubectl --context k3d-invoiceflow -n invoiceflow logs deploy/worker --since=1h | grep -i "<invoice-id>"
kubectl --context k3d-invoiceflow -n invoiceflow logs deploy/api --since=1h | grep -i "<invoice-id>"
```

Look for:

- Published / drained outbox messages
- `Failed processing invoice … (attempt N)`
- `Scheduled retry via retry.…`
- `Published message to DLQ`
- Ollama / fake extractor errors (no prompt or document text should appear)

## 6. Classify the failure

| Kind | Typical signal | Operator action |
| --- | --- | --- |
| Business | Status `ExtractionFailed`, reason `extraction.*` catalogue code, no DLQ storm | User: retry extraction or enter manually |
| Technical exhausted | DLQ + reason about repeated attempts | Check Ollama (`make ollama-check`), storage, DB; then retry extraction |
| Stuck `Processing` | Status `Processing` beyond ~30 minutes | Wait for sweeper or inspect worker crash loops |
| Lost publish | Outbox unpublished while RabbitMQ was down | Fix RabbitMQ; outbox drain catches up automatically |

## Related

- Error codes: [`docs/error-codes.md`](../error-codes.md)
- Failure drills: [`stage5-failure-experiments.md`](stage5-failure-experiments.md)
- Platform alert conditions: [`observability-alerts.md`](observability-alerts.md)
