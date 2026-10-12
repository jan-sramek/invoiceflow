# Log events

Every log message we write ourselves is a source-generated `[LoggerMessage]` method with a stable event id and name. Ids never change meaning once released; retired ids are not reused.

## Id ranges

| Range | Area |
| --- | --- |
| 1–999 | Platform: hosting, health, tenancy and messaging plumbing |
| 1000–1999 | Invoices |
| 2000–2999 | Extraction |

New areas get the next free thousand.

## Events

| Id | Name | Level | Source | Fields |
| --- | --- | --- | --- | --- |
| 1 | ServiceStarted | Information | ServiceDefaults | `ServiceName`, `ServiceVersion`, `Environment` |
| 100 | CatalogMigrated | Information | Infrastructure | `SchemaVersion` |
| 101 | TenantMigrated | Information | Infrastructure | `TenantId`, `SchemaVersion` |
| 102 | TenantMigrationFailed | Error | Infrastructure | `TenantId`, exception |
| 103 | SchemaMigrationFinished | Information, Warning if any tenant failed | Infrastructure | `Succeeded`, `Failed` |
| 200 | UnexpectedError | Error | Api | exception |

## Framework events we rely on

| Category | Event | Notes |
| --- | --- | --- |
| `Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware` | 9 | One line per request: `Method`, `Path`, `StatusCode`, `Duration`, and `ErrorCode` on error responses. No query string, headers or bodies. Health checks are skipped. |

## Rules

- Log identifiers, statuses, durations, counts and error codes. Never invoice field values, document text, prompts, tokens or connection strings.
- Values that may carry personal or confidential data get a data classification attribute; the redactor erases them.
- Trace and span ids come from the logging scope; don't add them as message fields.
