import { HttpHeaders, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { InvoiceUploadStore, MAX_PARALLEL_UPLOADS, MAX_UPLOAD_BYTES, needsNewKey, validateFile } from './invoice-upload.store';
import { ApiError } from '@core/http';

const pdf = (name: string) => new File(['%PDF-1.7'], name, { type: 'application/pdf' });

const detail = (id: string) => ({
  id,
  status: 'Uploaded',
  fields: {},
  document: { id: 'd', originalFileName: 'x.pdf', contentType: 'application/pdf', sizeBytes: 8 },
  duplicate: null,
  uploadedBy: { type: 'User', id: 'dev-clerk', name: 'Clara Clerk' },
  uploadedAt: '2026-01-01T00:00:00Z',
  submittedBy: null,
  paidOn: null,
  paymentReference: null,
});

describe('InvoiceUploadStore', () => {
  let store: InstanceType<typeof InvoiceUploadStore>;
  let http: HttpTestingController;

  const uploads = () => http.match((r) => r.method === 'POST' && r.url === '/api/v1/invoices');
  const keyOf = (request: TestRequest) => request.request.headers.get('Idempotency-Key');

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        InvoiceUploadStore,
      ],
    });
    store = TestBed.inject(InvoiceUploadStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uploads at most three files at a time, each with its own Idempotency-Key', () => {
    store.addFiles(['a', 'b', 'c', 'd', 'e'].map((n) => pdf(`${n}.pdf`)));

    const first = uploads();
    expect(first.length).toBe(MAX_PARALLEL_UPLOADS);
    expect(new Set(first.map(keyOf)).size).toBe(3);
    expect(first.every((r) => !!keyOf(r))).toBeTrue();
    expect(store.items().filter((i) => i.state === 'uploading').length).toBe(3);
    expect(store.items().filter((i) => i.state === 'queued').length).toBe(2);

    first[0].flush(detail('1'), { status: 202, statusText: 'Accepted', headers: new HttpHeaders({ ETag: '"v1"' }) });

    // A free slot starts the next file, not both.
    const next = uploads();
    expect(next.length).toBe(1);
    expect(store.doneCount()).toBe(1);

    for (const request of [...first.slice(1), ...next]) {
      request.flush(detail('x'), { status: 202, statusText: 'Accepted' });
    }
    const last = uploads();
    expect(last.length).toBe(1);
    last[0].flush(detail('y'), { status: 202, statusText: 'Accepted' });

    expect(store.doneCount()).toBe(5);
    expect(store.uploading()).toBeFalse();
  });

  it('keeps the created invoice on a finished item', () => {
    store.addFiles([pdf('a.pdf')]);
    uploads()[0].flush(detail('inv-1'), { status: 202, statusText: 'Accepted' });

    expect(store.items()[0].state).toBe('done');
    expect(store.items()[0].invoice?.id).toBe('inv-1');
  });

  it('lists files the server would refuse as failed, without sending them', () => {
    const text = new File(['hello'], 'notes.txt', { type: 'text/plain' });
    const empty = new File([], 'empty.pdf', { type: 'application/pdf' });
    store.addFiles([text, empty]);

    expect(uploads().length).toBe(0);
    expect(store.items().map((i) => i.error?.code)).toEqual(['upload.not_pdf', 'upload.not_pdf']);
    expect(store.failedCount()).toBe(2);
  });

  it('accepts a PDF whose type the browser could not tell', () => {
    expect(validateFile(new File(['x'], 'scan.PDF', { type: '' }))).toBeNull();
  });

  it('rejects files over the size limit with the limit as a parameter', () => {
    const big = { name: 'big.pdf', type: 'application/pdf', size: MAX_UPLOAD_BYTES + 1 } as File;

    const error = validateFile(big);

    expect(error?.code).toBe('upload.too_large');
    expect(error?.params['maxBytes']).toBe(MAX_UPLOAD_BYTES);
  });

  it('keeps a failure per file and carries on with the others', () => {
    store.addFiles([pdf('a.pdf'), pdf('b.pdf')]);
    const [a, b] = uploads();

    a.flush({ code: 'upload.not_pdf', status: 415 }, { status: 415, statusText: 'Unsupported' });
    b.flush(detail('2'), { status: 202, statusText: 'Accepted' });

    expect(store.items().map((i) => i.state)).toEqual(['failed', 'done']);
    expect(store.items()[0].error?.code).toBe('upload.not_pdf');
  });

  it('retries with the same key after a server failure, so no second invoice is created', () => {
    store.addFiles([pdf('a.pdf')]);
    const [request] = uploads();
    const key = keyOf(request);
    request.flush(null, { status: 503, statusText: 'Unavailable' });

    store.retry(store.items()[0].id);

    const [again] = uploads();
    expect(keyOf(again)).toBe(key);
    again.flush(detail('1'), { status: 202, statusText: 'Accepted' });
    expect(store.doneCount()).toBe(1);
  });

  it('retries with a new key after a client error, since the same request would fail again', () => {
    store.addFiles([pdf('a.pdf')]);
    const [request] = uploads();
    const key = keyOf(request);
    request.flush({ code: 'idempotency.key_reused' }, { status: 422, statusText: 'Unprocessable' });

    store.retry(store.items()[0].id);

    const [again] = uploads();
    expect(keyOf(again)).not.toBe(key);
    again.flush(detail('1'), { status: 202, statusText: 'Accepted' });
  });

  it('removes a file that is not being sent but never one that is', () => {
    store.addFiles([pdf('a.pdf'), pdf('b.pdf'), pdf('c.pdf'), pdf('d.pdf')]);
    const queued = store.items().find((i) => i.state === 'queued');
    const sending = store.items().find((i) => i.state === 'uploading');

    store.remove(sending!.id);
    expect(store.items().length).toBe(4);

    store.remove(queued!.id);
    expect(store.items().length).toBe(3);

    // The removed file is never sent, even when a slot frees up.
    for (const request of uploads()) {
      request.flush(detail('x'), { status: 202, statusText: 'Accepted' });
    }
    expect(uploads().length).toBe(0);
  });

  it('clears finished items and keeps those in progress', () => {
    store.addFiles([pdf('a.pdf'), pdf('b.pdf'), pdf('c.pdf'), pdf('d.pdf')]);
    const [first] = uploads();
    first.flush(detail('1'), { status: 202, statusText: 'Accepted' });
    uploads(); // the fourth file started

    store.clearFinished();

    expect(store.items().length).toBe(3);
    expect(store.items().every((i) => i.state === 'uploading')).toBeTrue();
    for (const request of http.match(() => true)) {
      request.flush(detail('x'), { status: 202, statusText: 'Accepted' });
    }
  });
});

describe('needsNewKey', () => {
  const error = (status: number) => new ApiError({ kind: 'problem', status, code: 'x' });

  it('keeps the key after lost connections, timeouts, throttling and server errors', () => {
    for (const status of [0, 408, 429, 500, 502, 503]) {
      expect(needsNewKey(error(status))).withContext(String(status)).toBeFalse();
    }
  });

  it('takes a new key after other client errors', () => {
    for (const status of [400, 409, 413, 415, 422]) {
      expect(needsNewKey(error(status))).withContext(String(status)).toBeTrue();
    }
  });
});
