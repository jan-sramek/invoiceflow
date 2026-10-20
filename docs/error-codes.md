# Error codes

Every error response is `application/problem+json` ([RFC 9457](https://www.rfc-editor.org/rfc/rfc9457)):

```json
{
  "type": "https://github.com/jan-sramek/invoiceflow/blob/main/docs/error-codes.md#invoice.invalid_transition",
  "title": "Invalid status transition",
  "status": 409,
  "detail": "invoice.invalid_transition (from=Paid, action=Cancel)",
  "instance": "/invoices/01a1.../cancel",
  "code": "invoice.invalid_transition",
  "params": { "from": "Paid", "action": "Cancel" },
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

- `code` is the stable identifier. Clients branch and translate on `code` and `params`, never on `title` or `detail`.
- `title` is constant per code; `detail` is English text for developers and may change.
- `errors` lists field errors as `{ pointer, code, params }`, where `pointer` is a JSON Pointer into the request body or the resource.
- `traceId` equals the `X-Trace-Id` response header. Quote it when reporting a problem.
- 5xx responses carry a generic `detail` and nothing about the cause.
- 503 responses carry `Retry-After` in seconds.

Codes are `area.reason` in lower snake case. A released code keeps its meaning; a new meaning gets a new code.

## Request

| Code | Status | Meaning | Params | What to do |
| --- | --- | --- | --- | --- |
| <a id="request.malformed"></a>`request.malformed` | 400 | The body is missing or is not valid JSON. | | Fix the request body. |
| <a id="request.invalid"></a>`request.invalid` | 4xx | The request was refused for a reason with no specific code. | | Check the method, headers and URL. |
| <a id="request.not_found"></a>`request.not_found` | 404 | No endpoint exists at this URL. | | Check the URL. |
| <a id="request.method_not_allowed"></a>`request.method_not_allowed` | 405 | The endpoint exists but not for this method. | | Use a method listed in `Allow`. |
| <a id="request.unsupported_media_type"></a>`request.unsupported_media_type` | 415 | The body has a content type the endpoint does not accept. | | Send `application/json`. |

## Validation

| Code | Status | Meaning | Params | What to do |
| --- | --- | --- | --- | --- |
| <a id="validation.failed"></a>`validation.failed` | 400 | One or more fields are invalid; `errors` lists them. | | Fix each field in `errors`. |
| <a id="validation.invalid_format"></a>`validation.invalid_format` | field | The value has the wrong type or format. | | Send the type the API documents. |
| <a id="validation.unknown_property"></a>`validation.unknown_property` | field | The body has a property the endpoint does not know. | | Remove it or fix its spelling. |
| <a id="validation.required"></a>`validation.required` | 400, field | A value is missing or blank. | `field` | Provide a value. |
| <a id="validation.max_length"></a>`validation.max_length` | 400 | A value is longer than allowed. | `field`, `max` | Shorten it to `max` characters. |
| <a id="validation.out_of_range"></a>`validation.out_of_range` | field | A number is outside the allowed range. | `field`, `min`, `max` | Send a value from `min` to `max`. |
| <a id="validation.range_reversed"></a>`validation.range_reversed` | field | The end of a range is before its start. | `field`, `from` | Swap the values or correct one of them. |
| <a id="validation.not_allowed"></a>`validation.not_allowed` | field | The value is not one of the allowed values. | `field`, `allowed` | Send one of `allowed`. |
| <a id="validation.currency_invalid"></a>`validation.currency_invalid` | 400 | The currency is not an ISO 4217 code. | | Use a three-letter code such as `EUR`. |
| <a id="validation.amount_precision"></a>`validation.amount_precision` | 400 | An amount has too many decimal places. | `maxDecimals` | Round to `maxDecimals` places. |
| <a id="validation.amount_out_of_range"></a>`validation.amount_out_of_range` | 400 | An amount is larger than `max`, positive or negative. | `max` | Enter a smaller amount. |
| <a id="validation.must_be_positive"></a>`validation.must_be_positive` | field | An amount must be greater than zero. | | Enter a positive amount. |
| <a id="validation.total_mismatch"></a>`validation.total_mismatch` | field | The total is not net amount plus VAT. | | Correct the amounts. |
| <a id="validation.date_in_future"></a>`validation.date_in_future` | field | The date is later than today. | | Correct the date. |
| <a id="validation.due_date_before_invoice_date"></a>`validation.due_date_before_invoice_date` | field | The due date is before the invoice date. | | Correct one of the dates. |

A status of *field* means the code appears only inside `errors`.

## Invoices

| Code | Status | Meaning | Params | What to do |
| --- | --- | --- | --- | --- |
| <a id="invoice.not_found"></a>`invoice.not_found` | 404 | No invoice with this id exists for your organisation. | | Check the link, or go back to the invoice list. |
| <a id="invoice.invalid_transition"></a>`invoice.invalid_transition` | 409 | The invoice's status does not allow this action. | `from`, `action` | Reload the invoice and choose an allowed action. |
| <a id="invoice.not_ready_for_submission"></a>`invoice.not_ready_for_submission` | 422 | Fields needed for submission are missing or inconsistent; `errors` lists them. | | Complete the fields in `errors`, then submit again. |
| <a id="invoice.payment_date_in_future"></a>`invoice.payment_date_in_future` | 422 | The payment date is later than today. | | Enter the date the payment was made. |
| <a id="invoice.unresolved_duplicate"></a>`invoice.unresolved_duplicate` | 422 | The invoice is flagged as a possible duplicate and that has not been resolved. | | Confirm it is not a duplicate, or cancel it. |
| <a id="approval.own_invoice"></a>`approval.own_invoice` | 403 | Nobody may approve or reject an invoice they submitted. | | Ask another approver. |

## Upload

| Code | Status | Meaning | Params | What to do |
| --- | --- | --- | --- | --- |
| <a id="upload.too_large"></a>`upload.too_large` | 413 | The file is larger than the allowed maximum. | `maxBytes` | Send a PDF of at most `maxBytes` bytes. |
| <a id="upload.not_pdf"></a>`upload.not_pdf` | 415 | The file is missing or is not a PDF. | | Send a PDF file. |

## Idempotency

| Code | Status | Meaning | Params | What to do |
| --- | --- | --- | --- | --- |
| <a id="idempotency.key_required"></a>`idempotency.key_required` | 400 | The upload had no `Idempotency-Key` header. | | Send a unique key per upload attempt. |
| <a id="idempotency.in_progress"></a>`idempotency.in_progress` | 409 | The same key is still being processed. | | Wait briefly and retry with the same key. |
| <a id="idempotency.key_reused"></a>`idempotency.key_reused` | 422 | The same key was used for a different file. | | Use a new key for the new file. |

## Access

| Code | Status | Meaning | Params | What to do |
| --- | --- | --- | --- | --- |
| <a id="auth.user_required"></a>`auth.user_required` | 401 | The request needs a signed-in user. | | Sign in with Keycloak (Bearer access token). When `Authentication:DevelopmentUsers` is on, you may send `X-Development-User: clerk`, `approver` or `finance` instead. |

## Concurrency and availability

| Code | Status | Meaning | Params | What to do |
| --- | --- | --- | --- | --- |
| <a id="concurrency.if_match_required"></a>`concurrency.if_match_required` | 428 | A write came without the `ETag` of the version it changes. | | Send the `ETag` from the last read as `If-Match`. |
| <a id="concurrency.stale_version"></a>`concurrency.stale_version` | 412 | Someone changed the resource after you read it. | | Reload, reapply your change and retry. |
| <a id="tenant.unavailable"></a>`tenant.unavailable` | 503 | Your organisation's data store is not reachable. | | Retry after `Retry-After` seconds. |
| <a id="tenant.maintenance"></a>`tenant.maintenance` | 503 | Your organisation's data store is under maintenance (schema behind or last migration failed). | | Wait for migration to finish, then retry after `Retry-After` seconds. |
| <a id="storage.unavailable"></a>`storage.unavailable` | 503 | Document storage is not reachable. | | Retry after `Retry-After` seconds. |
| <a id="server.unexpected"></a>`server.unexpected` | 500 | An unexpected error occurred. | | Report it with the `traceId`. |

## Extraction

Stored on the invoice when automatic extraction fails (not returned as HTTP Problem Details from the API).

| Code | Status | Meaning | Params | What to do |
| --- | --- | --- | --- | --- |
| <a id="extraction.no_text_layer"></a>`extraction.no_text_layer` | — | The PDF has no text layer (typically a scan). | | Enter fields manually or cancel. OCR is not supported yet. |
| <a id="extraction.too_many_pages"></a>`extraction.too_many_pages` | — | The PDF has more pages than the configured limit. | | Upload a shorter document or enter fields manually. |
| <a id="extraction.pdf_unreadable"></a>`extraction.pdf_unreadable` | — | The PDF is encrypted or could not be parsed. | | Re-export the PDF or enter fields manually. |
| <a id="extraction.invalid_model_output"></a>`extraction.invalid_model_output` | — | The model did not return usable field JSON. | | Retry extraction later or enter fields manually. |
| <a id="extraction.fake_failed"></a>`extraction.fake_failed` | — | The configurable fake extractor simulated a business failure (local/tests only). | | Retry extraction or enter fields manually. |
