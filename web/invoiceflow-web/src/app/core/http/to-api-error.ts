import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { ApiError, ApiFieldError, ClientErrorCode } from './api-error';

/** The longest `Retry-After` the client will honour or report, in seconds. */
export const MAX_RETRY_AFTER_SECONDS = 60;

/**
 * Turns anything an HTTP call can throw into an {@link ApiError}. Never throws, so it is safe in `catchError`.
 *
 * Understands problem documents (RFC 9457 with the API's `code`, `params`, `errors` and `traceId`),
 * error statuses without one, network failures, timeouts, aborts and plain exceptions.
 * Problem documents delivered as a `Blob` must be read first with {@link readBlobProblem}.
 */
export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) {
    return error;
  }
  if (error instanceof HttpErrorResponse) {
    return fromHttpError(error);
  }
  if (isNamed(error, 'TimeoutError')) {
    return new ApiError({
      kind: 'timeout',
      status: 0,
      code: ClientErrorCode.Timeout,
      detail: messageOf(error),
      cause: error,
    });
  }
  if (isNamed(error, 'AbortError')) {
    return new ApiError({
      kind: 'canceled',
      status: 0,
      code: ClientErrorCode.Canceled,
      detail: messageOf(error),
      cause: error,
    });
  }
  return new ApiError({
    kind: 'unknown',
    status: 0,
    code: ClientErrorCode.Unknown,
    detail: messageOf(error),
    cause: error,
  });
}

/**
 * `HttpClient` hands a `responseType: 'blob'` error body over as a `Blob`, even when it is a problem
 * document. Reads it and returns an equivalent response with the parsed body; any other value is returned as is.
 */
export async function readBlobProblem(error: unknown): Promise<unknown> {
  if (!(error instanceof HttpErrorResponse) || !(error.error instanceof Blob)) {
    return error;
  }
  let body: unknown = null;
  try {
    body = JSON.parse(await error.error.text());
  } catch {
    // Not JSON: leave the body empty so the status alone describes the error.
  }
  return new HttpErrorResponse({
    error: body,
    headers: error.headers,
    status: error.status,
    statusText: error.statusText,
    url: error.url ?? undefined,
  });
}

function fromHttpError(error: HttpErrorResponse): ApiError {
  const traceId = error.headers?.get('X-Trace-Id') ?? undefined;
  const retryAfterSeconds = parseRetryAfter(error.headers);

  if (error.status === 0) {
    return new ApiError({
      kind: 'network',
      status: 0,
      code: ClientErrorCode.Network,
      detail: error.message,
      traceId,
      cause: error,
    });
  }

  const body = parseBody(error.error);
  if (body && typeof body['code'] === 'string' && body['code'].length > 0) {
    return new ApiError({
      kind: 'problem',
      status: typeof body['status'] === 'number' ? body['status'] : error.status,
      code: body['code'],
      title: text(body['title']),
      detail: text(body['detail']),
      params: record(body['params']),
      errors: fieldErrors(body['errors']),
      traceId: text(body['traceId']) ?? traceId,
      retryAfterSeconds,
      cause: error,
    });
  }

  return new ApiError({
    kind: 'http',
    status: error.status,
    code: `http.${error.status}`,
    title: error.statusText || undefined,
    detail: error.message,
    traceId,
    retryAfterSeconds,
    cause: error,
  });
}

function parseBody(body: unknown): Record<string, unknown> | null {
  let value = body;
  if (typeof value === 'string') {
    try {
      value = JSON.parse(value);
    } catch {
      return null;
    }
  }
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null;
}

function fieldErrors(value: unknown): ApiFieldError[] {
  if (!Array.isArray(value)) {
    return [];
  }
  return value.flatMap((item): ApiFieldError[] => {
    if (typeof item !== 'object' || item === null) {
      return [];
    }
    const entry = item as Record<string, unknown>;
    if (typeof entry['code'] !== 'string' || entry['code'].length === 0) {
      return [];
    }
    return [
      {
        pointer: typeof entry['pointer'] === 'string' ? entry['pointer'] : '',
        code: entry['code'],
        params: record(entry['params']),
      },
    ];
  });
}

/** Seconds from a `Retry-After` header in either of its forms (delay or HTTP date); `undefined` when absent or invalid. */
export function parseRetryAfter(headers: HttpHeaders | null | undefined, now = Date.now()): number | undefined {
  const raw = headers?.get('Retry-After')?.trim();
  if (!raw) {
    return undefined;
  }
  let seconds: number;
  if (/^\d+$/.test(raw)) {
    seconds = Number(raw);
  } else {
    const date = Date.parse(raw);
    if (Number.isNaN(date)) {
      return undefined;
    }
    seconds = Math.ceil((date - now) / 1000);
  }
  return Math.min(Math.max(seconds, 0), MAX_RETRY_AFTER_SECONDS);
}

function record(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function text(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

function isNamed(error: unknown, name: string): boolean {
  return typeof error === 'object' && error !== null && (error as { name?: unknown }).name === name;
}

function messageOf(error: unknown): string | undefined {
  if (error instanceof Error) {
    return error.message;
  }
  return typeof error === 'string' ? error : undefined;
}
