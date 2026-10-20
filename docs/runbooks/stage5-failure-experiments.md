# Stage 5 failure experiments

Scripted drills for PLAN Stage 5 (**done when:** no invoice is lost or double-processed). Run on the k3d stack (`make stack-up`, `make rabbitmq-up`, apps up). Record date, operator, and pass/fail in your notes; this file is the procedure, not the evidence log.

Use the fake extractor for deterministic technical failures:

```yaml
# worker env (example)
FakeExtractor__FailureRate: "1"
FakeExtractor__FailureMode: "Technical"   # or Business
FakeExtractor__Delay: "00:00:05"
```

Unset `DocumentExtraction__ServiceUrl` while drilling so the fake is active.

---

## 1. Kill the worker mid-processing

**Setup:** `FakeExtractor__Delay=00:01:00` (or longer), upload one PDF, confirm status becomes `Processing` or message is unacked.

**Action:**

```bash
kubectl --context k3d-invoiceflow -n invoiceflow delete pod -l app.kubernetes.io/name=worker
```

**Expect:**

- Pod restarts (`terminationGracePeriodSeconds: 180`; consumer cancels, then drains or leaves unacked).
- Message is requeued or finished; invoice is **not** left permanently without a path forward (either completes, retries via TTL queues, or sweeper re-queues if stuck in `Processing`).
- No duplicate `NeedsReview` / double field apply (`processed_messages`).

**Pass:** invoice reaches a terminal processing outcome or is visibly re-driven without duplicate business effects.

---

## 2. Stop RabbitMQ during uploads (outbox buffers)

**Action:**

```bash
kubectl --context k3d-invoiceflow -n invoiceflow-infra scale statefulset rabbitmq --replicas=0
```

Upload several invoices (API should still return 202).

**Expect:**

- Uploads succeed; `outbox_messages.published_at` stays null.
- API logs show outbox drain failures / tenant back-off, not request 500s from publish.

**Restore:**

```bash
kubectl --context k3d-invoiceflow -n invoiceflow-infra scale statefulset rabbitmq --replicas=1
# wait until rabbitmq-0 is ready; re-run topology/users if needed
```

**Pass:** after RabbitMQ is healthy, outbox rows gain `published_at` and workers process the invoices.

---

## 3. Duplicate delivery

**Setup:** process one invoice to `NeedsReview`. Note `outbox_messages.id` (= RabbitMQ `message_id`) and `processed_messages`.

**Action:** republish the same payload with the **same** `message_id` to `invoiceflow.events` / `invoice.uploaded` (rabbitmqadmin / a small publish script), or temporarily re-insert an unpublished outbox clone with a **new** id only if testing sweeper (different path).

Preferred: republish with identical body and `message_id` header equal to the original outbox id.

**Expect:**

- Worker logs “Skipping duplicate delivery…” (or status guard skip).
- Invoice status unchanged; no second audit “success” path that double-applies fields.

**Pass:** one logical outcome despite two deliveries.

---

## 4. Poison message → DLQ

**Action:** publish invalid JSON to `invoiceflow.events` with routing key `invoice.uploaded` (or a body missing ids).

**Expect:**

- Worker logs invalid payload / missing ids.
- Message appears on `invoice-processing.dlq` (not silently acked-and-dropped without DLQ).
- Healthy invoices continue to process.

**Pass:** DLQ depth increases by one; processing queue drains normally.

---

## 5. Scale workers to 3

**Action:**

```bash
kubectl --context k3d-invoiceflow -n invoiceflow scale deployment worker --replicas=3
```

Upload a burst of invoices (fake extractor with short delay).

**Expect:**

- Three consumer pods; queue depth falls.
- Prefetch is 1 per consumer — no thundering herd on a single message.
- Still no double-apply (`processed_messages` + status guards).

**Cleanup:** scale back to 1 when finished.

**Pass:** all invoices complete once; no conflicting statuses across tenants.

---

## After the drills

1. Scale RabbitMQ and worker back to normal.
2. Clear DLQ only after recording the poison test (ops judgment).
3. Remove fake-extractor failure env vars / restore Ollama URL.
4. Spot-check one invoice with [`find-failed-invoice.md`](find-failed-invoice.md).
