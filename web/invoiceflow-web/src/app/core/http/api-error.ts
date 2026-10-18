/** One entry of a problem response's `errors` list. `pointer` is a JSON Pointer into the request body or resource. */
export interface ApiFieldError {
  readonly pointer: string;
  readonly code: string;
  readonly params: Readonly<Record<string, unknown>>;
}

/**
 * How the error arose:
 * - `problem`: the server answered with a problem document (it has a stable `code`);
 * - `http`: the server answered with an error status but no problem document (proxy page, gateway);
 * - `network`: no response at all;
 * - `timeout`, `canceled`: the client gave up or was told to stop;
 * - `unknown`: anything else, including bugs.
 */
export type ApiErrorKind = 'problem' | 'http' | 'network' | 'timeout' | 'canceled' | 'unknown';

/** Codes the client makes up when the server gave none. Server codes are documented in docs/error-codes.md. */
export const ClientErrorCode = {
  Network: 'client.network',
  Timeout: 'client.timeout',
  Canceled: 'client.canceled',
  Unknown: 'client.unknown',
} as const;

/** Server codes the client treats specially. */
export const ServerErrorCode = {
  StaleVersion: 'concurrency.stale_version',
  IdempotencyInProgress: 'idempotency.in_progress',
  ValidationFailed: 'validation.failed',
  NotReadyForSubmission: 'invoice.not_ready_for_submission',
  NotFound: 'invoice.not_found',
} as const;

export interface ApiErrorInit {
  readonly kind: ApiErrorKind;
  readonly status: number;
  readonly code: string;
  readonly title?: string;
  readonly detail?: string;
  readonly params?: Readonly<Record<string, unknown>>;
  readonly errors?: readonly ApiFieldError[];
  readonly traceId?: string;
  readonly retryAfterSeconds?: number;
  readonly cause?: unknown;
}

/**
 * The one error type the UI sees from the API. Branch and translate on `code` and `params`,
 * never on `title` or `detail`; quote `traceId` when reporting a problem.
 */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  /** HTTP status, or 0 when there was no response. */
  readonly status: number;
  readonly code: string;
  readonly title?: string;
  readonly detail?: string;
  readonly params: Readonly<Record<string, unknown>>;
  readonly errors: readonly ApiFieldError[];
  readonly traceId?: string;
  readonly retryAfterSeconds?: number;
  override readonly cause?: unknown;

  constructor(init: ApiErrorInit) {
    super(init.detail ?? init.title ?? init.code);
    this.name = 'ApiError';
    this.kind = init.kind;
    this.status = init.status;
    this.code = init.code;
    this.title = init.title;
    this.detail = init.detail;
    this.params = init.params ?? {};
    this.errors = init.errors ?? [];
    this.traceId = init.traceId;
    this.retryAfterSeconds = init.retryAfterSeconds;
    this.cause = init.cause;
    // Keeps instanceof working if the build ever targets ES5.
    Object.setPrototypeOf(this, ApiError.prototype);
  }

  get isServerError(): boolean {
    return this.status >= 500;
  }

  get hasFieldErrors(): boolean {
    return this.errors.length > 0;
  }

  is(code: string): boolean {
    return this.code === code;
  }
}

export function isApiError(value: unknown): value is ApiError {
  return value instanceof ApiError;
}
