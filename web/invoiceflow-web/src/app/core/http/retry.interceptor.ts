import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { retry, timer } from 'rxjs';
import { AppConfigService } from '../config';
import { ServerErrorCode } from './api-error';
import { DISABLE_RETRY, isApiRequest } from './http-context';
import { parseRetryAfter } from './to-api-error';

export const IDEMPOTENCY_KEY_HEADER = 'Idempotency-Key';

/** Retries after the first attempt, so a request is sent at most `1 + MAX_RETRIES` times. */
export const MAX_RETRIES = 2;
const BASE_DELAY_MS = 400;
const MAX_DELAY_MS = 10_000;

/**
 * Only requests that are safe to send twice: reads, and uploads that carry an `Idempotency-Key`
 * (the server then returns the first result instead of creating a second invoice).
 * Writes guarded by `If-Match` are never retried: a retry after a lost response would meet its own change.
 */
export function isRetryableRequest(request: HttpRequest<unknown>): boolean {
  if (request.method === 'GET' || request.method === 'HEAD') {
    return true;
  }
  return request.method === 'POST' && request.headers.has(IDEMPOTENCY_KEY_HEADER);
}

/** Failures that are likely gone a moment later: no response, timeouts, throttling, gateway and availability errors. */
export function isTransientFailure(request: HttpRequest<unknown>, error: HttpErrorResponse): boolean {
  switch (error.status) {
    case 0:
    case 408:
    case 429:
    case 502:
    case 503:
    case 504:
      return true;
    case 409:
      // The same key is still being processed; waiting briefly and asking again is what the API prescribes.
      return (
        request.headers.has(IDEMPOTENCY_KEY_HEADER) &&
        (error.error as { code?: unknown } | null)?.code === ServerErrorCode.IdempotencyInProgress
      );
    default:
      return false;
  }
}

/** Waits `Retry-After` when the server gave it, otherwise 400 ms, 800 ms, ... with up to 25 % jitter; capped at 10 s. */
export function retryDelayMs(retryNumber: number, retryAfterSeconds?: number, random = Math.random): number {
  if (retryAfterSeconds !== undefined) {
    return Math.min(retryAfterSeconds * 1000, MAX_DELAY_MS);
  }
  const exponential = BASE_DELAY_MS * 2 ** (retryNumber - 1);
  return Math.min(Math.round(exponential * (1 + random() * 0.25)), MAX_DELAY_MS);
}

export const retryInterceptor: HttpInterceptorFn = (request, next) => {
  const config = inject(AppConfigService);
  if (
    !isApiRequest(request, config.apiBaseUrl) ||
    request.context.get(DISABLE_RETRY) ||
    !isRetryableRequest(request)
  ) {
    return next(request);
  }

  return next(request).pipe(
    retry({
      count: MAX_RETRIES,
      delay: (error: unknown, retryNumber) => {
        if (!(error instanceof HttpErrorResponse) || !isTransientFailure(request, error)) {
          throw error;
        }
        return timer(retryDelayMs(retryNumber, parseRetryAfter(error.headers)));
      },
    }),
  );
};
