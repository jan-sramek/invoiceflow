# Runbook: observability alert conditions (Stage 8)

**NFR-OBS-01 (partial).** Documents *when* to page or investigate. Grafana alert rules and notification channels are Stage 9; use Explore and these conditions manually until then.

Signals come from **Loki** (logs), **Tempo** (traces), and **Prometheus** (metrics) after `make observability-up`. The Aspire dashboard (`make dashboard-url`) still mirrors OTLP for quick local checks.

## Prerequisites

- Stack running: `make observability-up`, apps deployed (`make app-up` or full stack).
- Grafana: `http://grafana.invoiceflow.localhost:8080` (`make grafana-password`).

## API availability and errors

| Condition | Signal (Prometheus / Loki) | Investigate when |
| --- | --- | --- |
| High **5xx rate** on the API | `http_server_request_duration_seconds_count` (or OTLP `http.server.request.duration`) with `http_response_status_code` ≥ 500, `service.name` = `InvoiceFlow.Api`; Loki `{namespace="invoiceflow", app="api"} \|= "error"` | Sustained **> 5%** of request rate for **5m**, or any spike during a demo |
| API **not ready** | Catalog `/health/ready` failing; Loki readiness probe failures | Readiness not **Ready** for **> 2m** |
| **Latency** regression | Histogram `http.server.request.duration` p95 for API **> 2s** for **10m** while load is normal | User reports slowness or upload timeouts |

## Worker and invoice pipeline

| Condition | Signal | Investigate when |
| --- | --- | --- |
| **Worker down** or crash-loop | Loki `{namespace="invoiceflow", app="worker"}` absent or repeated panic lines; missing worker spans in Tempo for recent uploads | No worker logs for **> 5m** while uploads are attempted |
| **Queue backlog** | RabbitMQ `invoice-processing` depth (see [`find-failed-invoice.md`](find-failed-invoice.md)); worker consume rate ≈ 0 in traces | Queue messages **growing** for **> 10m** with healthy RabbitMQ |
| **DLQ growth** | `invoice-processing.dlq` message count increasing | **Any** sustained increase over **15m** or **> 10** messages after a drill |
| **Extraction failure storm** | Loki worker `\|= "Failed processing invoice"` rate; audit events `invoice.extraction_failed` | **> 10** failures in **5m** for one tenant or globally |

## Tenancy and dependencies

| Condition | Signal (meter `InvoiceFlow.Tenancy`) | Investigate when |
| --- | --- | --- |
| **Tenant DB failures** | `tenant_db.failures` rate by `tenant.id` | **> 0.1/s** for one tenant for **5m**, or catalog still ready but tenant returns `503` |
| **Breaker open** | `tenant_db.breaker_open` = 1 | Stays **1** for **> 2m** for a tenant users need |
| **Ollama / extractor unavailable** | Worker logs Ollama HTTP errors; missing Ollama client spans | Correlates with extraction failures; run `make ollama-check` |

## Observability platform health

| Condition | Signal | Investigate when |
| --- | --- | --- |
| **No traces in Tempo** | Grafana → Explore → Tempo: empty search for `service.name` = `InvoiceFlow.Api` after traffic | No spans for **> 15m** while API is in use; run `make telemetry-check` |
| **No app metrics in Prometheus** | Query `{service_name=~"InvoiceFlow.*"}` or tenancy counters empty | Empty for **> 15m** with active API; check collector pods in `invoiceflow-collector` |
| **Log ingestion gap** | Loki `{namespace="invoiceflow"}` volume drops to zero | No lines for **> 10m** while pods are running |
| **Collector unhealthy** | `kubectl -n invoiceflow-collector get pods`; collector logs export errors | Pod **Not Ready** or repeated **export** / **remote_write** errors |

## Trace-led triage (invoice id)

For a single failed invoice, prefer the flow in [`find-failed-invoice.md`](find-failed-invoice.md): Tempo trace → outbox SQL → RabbitMQ → Loki. Alert conditions above catch **systemic** issues; that runbook covers **single-invoice** reconstruction.

## Local verification

```bash
make observability-up
make telemetry-check          # traces → Tempo (+ Aspire)
make grafana-password
# Grafana Explore: Tempo (service telemetry-check), Prometheus (up), Loki ({namespace="invoiceflow"})
```

## Related

- Invoice failure path: [`find-failed-invoice.md`](find-failed-invoice.md)
- On-prem tenancy metrics: [`stage6-onprem-isolation.md`](stage6-onprem-isolation.md)
