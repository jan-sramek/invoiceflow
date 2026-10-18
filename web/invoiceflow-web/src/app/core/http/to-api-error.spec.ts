import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { ApiError, ClientErrorCode, isApiError } from './api-error';
import { MAX_RETRY_AFTER_SECONDS, parseRetryAfter, readBlobProblem, toApiError } from './to-api-error';

function httpError(
  status: number,
  body: unknown,
  headers: Record<string, string> = {},
  statusText = '',
): HttpErrorResponse {
  return new HttpErrorResponse({
    status,
    statusText,
    error: body,
    headers: new HttpHeaders(headers),
    url: '/api/v1/invoices',
  });
}

describe('toApiError', () => {
  describe('problem documents', () => {
    const problem = {
      type: 'https://example.test/docs#invoice.invalid_transition',
      title: 'Invalid status transition',
      status: 409,
      detail: 'invoice.invalid_transition (from=Paid, action=Cancel)',
      instance: '/invoices/1/cancel',
      code: 'invoice.invalid_transition',
      params: { from: 'Paid', action: 'Cancel' },
      traceId: '4bf92f3577b34da6a3ce929d0e0e4736',
    };

    it('maps code, params, title, detail, status and traceId', () => {
      const error = toApiError(httpError(409, problem));

      expect(error instanceof ApiError).toBeTrue();
      expect(error.kind).toBe('problem');
      expect(error.status).toBe(409);
      expect(error.code).toBe('invoice.invalid_transition');
      expect(error.params).toEqual({ from: 'Paid', action: 'Cancel' });
      expect(error.title).toBe('Invalid status transition');
      expect(error.detail).toContain('invoice.invalid_transition');
      expect(error.traceId).toBe('4bf92f3577b34da6a3ce929d0e0e4736');
      expect(error.errors).toEqual([]);
      expect(error.message).toBe(problem.detail);
    });

    it('prefers the traceId in the body and falls back to the X-Trace-Id header', () => {
      expect(toApiError(httpError(409, problem, { 'X-Trace-Id': 'from-header' })).traceId).toBe(
        problem.traceId,
      );
      const { traceId: _omitted, ...withoutTrace } = problem;
      expect(toApiError(httpError(409, withoutTrace, { 'X-Trace-Id': 'from-header' })).traceId).toBe(
        'from-header',
      );
      expect(toApiError(httpError(409, withoutTrace)).traceId).toBeUndefined();
    });

    it('defaults params and errors when the server sends none', () => {
      const error = toApiError(httpError(404, { code: 'invoice.not_found', status: 404 }));

      expect(error.params).toEqual({});
      expect(error.errors).toEqual([]);
      expect(error.hasFieldErrors).toBeFalse();
    });

    it('ignores params that are not an object', () => {
      expect(toApiError(httpError(400, { code: 'x', params: 'oops' })).params).toEqual({});
      expect(toApiError(httpError(400, { code: 'x', params: [1, 2] })).params).toEqual({});
      expect(toApiError(httpError(400, { code: 'x', params: null })).params).toEqual({});
    });

    it('takes the HTTP status when the body has none, or a wrong type', () => {
      expect(toApiError(httpError(422, { code: 'x' })).status).toBe(422);
      expect(toApiError(httpError(422, { code: 'x', status: '400' })).status).toBe(422);
    });

    it('parses a problem document delivered as text', () => {
      const error = toApiError(httpError(403, JSON.stringify({ code: 'approval.own_invoice' })));

      expect(error.kind).toBe('problem');
      expect(error.code).toBe('approval.own_invoice');
      expect(error.status).toBe(403);
    });

    it('is a plain HTTP error when the body has no usable code', () => {
      for (const body of [{ code: '' }, { code: 42 }, { title: 'x' }, [], 'plain text', null, undefined]) {
        const error = toApiError(httpError(500, body, {}, 'Internal Server Error'));
        expect(error.kind).withContext(JSON.stringify(body)).toBe('http');
        expect(error.code).toBe('http.500');
      }
    });
  });

  describe('field errors', () => {
    it('maps pointer, code and params', () => {
      const error = toApiError(
        httpError(400, {
          code: 'validation.failed',
          errors: [
            { pointer: '/supplierName', code: 'validation.required', params: { field: 'supplierName' } },
            { pointer: '/invoiceNumber', code: 'validation.max_length', params: { field: 'invoiceNumber', max: 50 } },
          ],
        }),
      );

      expect(error.hasFieldErrors).toBeTrue();
      expect(error.errors).toEqual([
        { pointer: '/supplierName', code: 'validation.required', params: { field: 'supplierName' } },
        { pointer: '/invoiceNumber', code: 'validation.max_length', params: { field: 'invoiceNumber', max: 50 } },
      ]);
    });

    it('keeps errors that point at the whole document with an empty pointer', () => {
      const error = toApiError(
        httpError(422, { code: 'invoice.not_ready_for_submission', errors: [{ code: 'validation.required' }] }),
      );

      expect(error.errors).toEqual([{ pointer: '', code: 'validation.required', params: {} }]);
    });

    it('drops entries that are malformed', () => {
      const error = toApiError(
        httpError(400, {
          code: 'validation.failed',
          errors: [null, 'x', 7, { pointer: '/a' }, { pointer: '/a', code: '' }, { pointer: '/b', code: 'validation.required' }],
        }),
      );

      expect(error.errors.map((e) => e.pointer)).toEqual(['/b']);
    });

    it('ignores an errors value that is not a list', () => {
      expect(toApiError(httpError(400, { code: 'x', errors: { a: 1 } })).errors).toEqual([]);
    });
  });

  describe('Retry-After', () => {
    it('reads delay seconds', () => {
      const error = toApiError(httpError(503, { code: 'storage.unavailable' }, { 'Retry-After': '7' }));

      expect(error.retryAfterSeconds).toBe(7);
    });

    it('reads it on responses without a problem document too', () => {
      expect(toApiError(httpError(503, '<html>', { 'Retry-After': '3' })).retryAfterSeconds).toBe(3);
    });

    it('is undefined when absent', () => {
      expect(toApiError(httpError(503, { code: 'storage.unavailable' })).retryAfterSeconds).toBeUndefined();
    });

    it('parses HTTP dates relative to now', () => {
      const now = Date.UTC(2026, 0, 1, 12, 0, 0);
      const headers = new HttpHeaders({ 'Retry-After': new Date(now + 5000).toUTCString() });

      expect(parseRetryAfter(headers, now)).toBe(5);
    });

    it('never goes negative, never exceeds the cap, and ignores garbage', () => {
      const now = Date.UTC(2026, 0, 1, 12, 0, 0);
      expect(parseRetryAfter(new HttpHeaders({ 'Retry-After': new Date(now - 9000).toUTCString() }), now)).toBe(0);
      expect(parseRetryAfter(new HttpHeaders({ 'Retry-After': '99999' }))).toBe(MAX_RETRY_AFTER_SECONDS);
      expect(parseRetryAfter(new HttpHeaders({ 'Retry-After': 'soon' }))).toBeUndefined();
      expect(parseRetryAfter(new HttpHeaders({ 'Retry-After': '  ' }))).toBeUndefined();
      expect(parseRetryAfter(null)).toBeUndefined();
    });
  });

  describe('responses that are not problem documents', () => {
    it('describes a gateway error by its status', () => {
      const error = toApiError(httpError(502, '<html>Bad gateway</html>', { 'X-Trace-Id': 'abc' }, 'Bad Gateway'));

      expect(error.kind).toBe('http');
      expect(error.status).toBe(502);
      expect(error.code).toBe('http.502');
      expect(error.title).toBe('Bad Gateway');
      expect(error.traceId).toBe('abc');
      expect(error.isServerError).toBeTrue();
    });

    it('reports a lost connection as a network error', () => {
      const error = toApiError(httpError(0, new ProgressEvent('error')));

      expect(error.kind).toBe('network');
      expect(error.status).toBe(0);
      expect(error.code).toBe(ClientErrorCode.Network);
      expect(error.isServerError).toBeFalse();
    });
  });

  describe('other failures', () => {
    it('maps a TimeoutError', () => {
      const timeout = new Error('Timeout has occurred');
      timeout.name = 'TimeoutError';

      const error = toApiError(timeout);

      expect(error.kind).toBe('timeout');
      expect(error.code).toBe(ClientErrorCode.Timeout);
      expect(error.cause).toBe(timeout);
    });

    it('maps an AbortError', () => {
      const error = toApiError(new DOMException('aborted', 'AbortError'));

      expect(error.kind).toBe('canceled');
      expect(error.code).toBe(ClientErrorCode.Canceled);
    });

    it('maps an ordinary exception without losing the cause', () => {
      const cause = new TypeError('boom');
      const error = toApiError(cause);

      expect(error.kind).toBe('unknown');
      expect(error.code).toBe(ClientErrorCode.Unknown);
      expect(error.detail).toBe('boom');
      expect(error.cause).toBe(cause);
    });

    it('survives values that are not errors', () => {
      for (const value of [undefined, null, 42, 'text', {}, []]) {
        const error = toApiError(value);
        expect(error.code).withContext(String(value)).toBe(ClientErrorCode.Unknown);
      }
      expect(toApiError('text').detail).toBe('text');
    });

    it('returns an ApiError unchanged', () => {
      const original = new ApiError({ kind: 'problem', status: 400, code: 'x' });

      expect(toApiError(original)).toBe(original);
    });
  });

  describe('ApiError', () => {
    it('is an Error that isApiError recognises', () => {
      const error = toApiError(httpError(404, { code: 'invoice.not_found' }));

      expect(error instanceof Error).toBeTrue();
      expect(isApiError(error)).toBeTrue();
      expect(isApiError(new Error('x'))).toBeFalse();
      expect(error.is('invoice.not_found')).toBeTrue();
      expect(error.is('other')).toBeFalse();
    });
  });
});

describe('readBlobProblem', () => {
  it('reads a problem document delivered as a blob', async () => {
    const body = new Blob([JSON.stringify({ code: 'storage.unavailable', traceId: 't1' })], {
      type: 'application/problem+json',
    });
    const read = await readBlobProblem(httpError(503, body, { 'Retry-After': '2' }));
    const error = toApiError(read);

    expect(error.kind).toBe('problem');
    expect(error.code).toBe('storage.unavailable');
    expect(error.traceId).toBe('t1');
    expect(error.retryAfterSeconds).toBe(2);
  });

  it('falls back to the status when the blob is not JSON', async () => {
    const read = await readBlobProblem(httpError(502, new Blob(['<html>'])));

    expect(toApiError(read).code).toBe('http.502');
  });

  it('passes other values through', async () => {
    const response = httpError(500, { code: 'x' });
    expect(await readBlobProblem(response)).toBe(response);
    expect(await readBlobProblem('x')).toBe('x');
  });
});
