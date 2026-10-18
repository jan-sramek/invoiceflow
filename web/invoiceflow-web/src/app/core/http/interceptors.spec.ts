import { HttpClient, HttpContext, HttpErrorResponse, HttpHeaders, HttpRequest } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiError } from './api-error';
import { DEV_USER_HEADER, DevUserService } from './dev-user.service';
import { shouldNotify } from './error.interceptor';
import { API_ERROR_NOTIFIER, SUPPRESS_ERROR_NOTIFICATION } from './http-context';
import { provideApiHttp } from './provide-api-http';
import {
  IDEMPOTENCY_KEY_HEADER,
  isRetryableRequest,
  isTransientFailure,
  MAX_RETRIES,
  retryDelayMs,
} from './retry.interceptor';

const problem = (status: number, body: unknown, headers: Record<string, string> = {}) =>
  new HttpErrorResponse({ status, error: body, headers: new HttpHeaders(headers) });

describe('retry policy', () => {
  const get = new HttpRequest('GET', '/api/v1/invoices');
  const put = new HttpRequest('PUT', '/api/v1/invoices/1/fields', {});
  const action = new HttpRequest('POST', '/api/v1/invoices/1/approve', null);
  const upload = new HttpRequest('POST', '/api/v1/invoices', new FormData(), {
    headers: new HttpHeaders({ [IDEMPOTENCY_KEY_HEADER]: 'k1' }),
  });
  const uploadWithoutKey = new HttpRequest('POST', '/api/v1/invoices', new FormData());

  it('retries reads and uploads with an Idempotency-Key only', () => {
    expect(isRetryableRequest(get)).toBeTrue();
    expect(isRetryableRequest(upload)).toBeTrue();
    expect(isRetryableRequest(uploadWithoutKey)).toBeFalse();
    expect(isRetryableRequest(put)).toBeFalse();
    expect(isRetryableRequest(action)).toBeFalse();
  });

  it('treats lost connections, throttling and availability errors as transient', () => {
    for (const status of [0, 408, 429, 502, 503, 504]) {
      expect(isTransientFailure(get, problem(status, null))).withContext(String(status)).toBeTrue();
    }
  });

  it('does not retry client errors or 500', () => {
    for (const status of [400, 401, 403, 404, 412, 413, 415, 422, 500]) {
      expect(isTransientFailure(get, problem(status, { code: 'x' }))).withContext(String(status)).toBeFalse();
    }
  });

  it('retries idempotency.in_progress only for uploads with a key', () => {
    const inProgress = problem(409, { code: 'idempotency.in_progress' });

    expect(isTransientFailure(upload, inProgress)).toBeTrue();
    expect(isTransientFailure(get, inProgress)).toBeFalse();
    expect(isTransientFailure(upload, problem(409, { code: 'invoice.invalid_transition' }))).toBeFalse();
  });

  it('backs off exponentially with a little jitter, and honours Retry-After', () => {
    expect(retryDelayMs(1, undefined, () => 0)).toBe(400);
    expect(retryDelayMs(2, undefined, () => 0)).toBe(800);
    expect(retryDelayMs(1, undefined, () => 1)).toBe(500);
    expect(retryDelayMs(1, 3)).toBe(3000);
    expect(retryDelayMs(1, 3600)).toBe(10_000);
    expect(retryDelayMs(20, undefined, () => 0)).toBe(10_000);
  });
});

describe('shouldNotify', () => {
  const make = (init: Partial<ConstructorParameters<typeof ApiError>[0]> = {}) =>
    new ApiError({ kind: 'problem', status: 409, code: 'invoice.invalid_transition', ...init });

  it('reports ordinary errors', () => {
    expect(shouldNotify(make(), false)).toBeTrue();
    expect(shouldNotify(make({ kind: 'network', status: 0, code: 'client.network' }), false)).toBeTrue();
  });

  it('leaves out suppressed, field, stale-version and cancelled errors', () => {
    expect(shouldNotify(make(), true)).toBeFalse();
    expect(
      shouldNotify(make({ errors: [{ pointer: '/a', code: 'validation.required', params: {} }] }), false),
    ).toBeFalse();
    expect(shouldNotify(make({ status: 412, code: 'concurrency.stale_version' }), false)).toBeFalse();
    expect(shouldNotify(make({ kind: 'canceled', code: 'client.canceled' }), false)).toBeFalse();
  });
});

describe('API interceptors', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let notified: ApiError[];

  beforeEach(() => {
    notified = [];
    localStorage.removeItem('invoiceflow.devUser');
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideApiHttp(),
        provideHttpClientTesting(),
        { provide: API_ERROR_NOTIFIER, useValue: (error: ApiError) => notified.push(error) },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    jasmine.clock().install();
  });

  afterEach(() => {
    jasmine.clock().uninstall();
    backend.verify();
  });

  it('sends the development user on API calls only', () => {
    TestBed.inject(DevUserService).select('approver');

    http.get('/api/v1/invoices').subscribe();
    http.get('https://other.example/api/v1/invoices').subscribe();

    expect(backend.expectOne('/api/v1/invoices').request.headers.get(DEV_USER_HEADER)).toBe('approver');
    expect(backend.expectOne('https://other.example/api/v1/invoices').request.headers.has(DEV_USER_HEADER)).toBeFalse();
  });

  it('turns an error response into an ApiError and reports it', async () => {
    const result = firstValueFrom(http.post('/api/v1/invoices/1/approve', null));
    backend
      .expectOne('/api/v1/invoices/1/approve')
      .flush({ code: 'approval.own_invoice', traceId: 't' }, { status: 403, statusText: 'Forbidden' });

    const error = await result.catch((e: unknown) => e);

    expect(error instanceof ApiError).toBeTrue();
    expect((error as ApiError).code).toBe('approval.own_invoice');
    expect(notified).toEqual([error as ApiError]);
  });

  it('does not report when the caller handles it', async () => {
    const context = new HttpContext().set(SUPPRESS_ERROR_NOTIFICATION, true);
    const result = firstValueFrom(http.post('/api/v1/invoices/1/approve', null, { context }));
    backend.expectOne('/api/v1/invoices/1/approve').flush({ code: 'x' }, { status: 409, statusText: 'Conflict' });

    await result.catch(() => undefined);

    expect(notified).toEqual([]);
  });

  it('reads a problem document delivered as a blob', async () => {
    const result = firstValueFrom(http.get('/api/v1/invoices/1/document', { responseType: 'blob' }));
    backend
      .expectOne('/api/v1/invoices/1/document')
      .flush(new Blob([JSON.stringify({ code: 'invoice.not_found' })]), { status: 404, statusText: 'Not Found' });

    const error = (await result.catch((e: unknown) => e)) as ApiError;

    expect(error.code).toBe('invoice.not_found');
  });

  it('retries a failed GET and succeeds when the server recovers', async () => {
    const result = firstValueFrom(http.get('/api/v1/invoices'));

    backend.expectOne('/api/v1/invoices').flush(null, { status: 503, statusText: 'Unavailable' });
    jasmine.clock().tick(1000);
    backend.expectOne('/api/v1/invoices').flush({ items: [] });

    expect(await result).toEqual({ items: [] });
    expect(notified).toEqual([]);
  });

  it('gives up after the retries and reports once', async () => {
    const result = firstValueFrom(http.get('/api/v1/invoices')).catch((e: unknown) => e);

    for (let attempt = 0; attempt <= MAX_RETRIES; attempt++) {
      backend.expectOne('/api/v1/invoices').flush({ code: 'storage.unavailable' }, { status: 503, statusText: 'x' });
      jasmine.clock().tick(11_000);
    }

    const error = (await result) as ApiError;
    expect(error.code).toBe('storage.unavailable');
    expect(notified.length).toBe(1);
  });

  it('does not retry a write', async () => {
    const result = firstValueFrom(http.put('/api/v1/invoices/1/fields', {})).catch((e: unknown) => e);

    backend.expectOne('/api/v1/invoices/1/fields').flush(null, { status: 503, statusText: 'x' });
    jasmine.clock().tick(11_000);

    expect(((await result) as ApiError).status).toBe(503);
    backend.expectNone('/api/v1/invoices/1/fields');
  });
});
