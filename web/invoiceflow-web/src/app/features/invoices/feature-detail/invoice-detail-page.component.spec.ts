import { HttpHeaders, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoLoader } from '@jsverse/transloco';
import { of } from 'rxjs';
import en from '../../../../../public/i18n/en.json';
import { InvoiceDetailPageComponent } from './invoice-detail-page.component';

class EnglishLoader implements TranslocoLoader {
  getTranslation() {
    return of(en);
  }
}

const ID = '22222222-2222-2222-2222-222222222222';

const detail = (over: object = {}, fields: object = {}) => ({
  id: ID,
  status: 'NeedsReview',
  fields: {
    supplierName: 'Acme s.r.o.',
    invoiceNumber: 'INV-1',
    invoiceDate: '2026-01-15',
    dueDate: '2026-02-15',
    currency: 'EUR',
    subtotal: 100,
    taxAmount: 21,
    totalAmount: 121,
    purchaseOrderReference: null,
    ...fields,
  },
  document: { id: 'd1', originalFileName: 'acme.pdf', contentType: 'application/pdf', sizeBytes: 2048 },
  duplicate: null,
  uploadedBy: { type: 'User', id: 'dev-clerk', name: 'Clara Clerk' },
  uploadedAt: '2026-01-16T10:00:00Z',
  submittedBy: null,
  paidOn: null,
  paymentReference: null,
  ...over,
});

const etag = (version: string) => ({ headers: new HttpHeaders({ ETag: `"${version}"` }) });
const emptyAudit = { items: [], page: 1, pageSize: 50, totalCount: 0 };

describe('InvoiceDetailPageComponent', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;
  let root: HTMLElement;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([{ path: 'invoices/:id', component: InvoiceDetailPageComponent }], withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTransloco({
          config: { availableLangs: ['en'], defaultLang: 'en', prodMode: true },
          loader: EnglishLoader,
        }),
      ],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  async function show(invoice: object, version = 'v1'): Promise<void> {
    await harness.navigateByUrl(`/invoices/${ID}`);
    TestBed.tick();
    http.expectOne(`/api/v1/invoices/${ID}`).flush(invoice, { status: 200, statusText: 'OK', ...etag(version) });
    TestBed.tick();
    http.expectOne((r) => r.url === `/api/v1/invoices/${ID}/audit`).flush(emptyAudit);
    await harness.fixture.whenStable();
    harness.detectChanges();
    root = harness.routeNativeElement as HTMLElement;
  }

  const button = (label: string) =>
    Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes(label)) as HTMLButtonElement | undefined;

  const input = (name: string) => root.querySelector(`input[formcontrolname=${name}]`) as HTMLInputElement;

  async function settle(): Promise<void> {
    TestBed.tick();
    await harness.fixture.whenStable();
    harness.detectChanges();
  }

  it('fills the form from the invoice and offers the actions of a reviewable invoice', async () => {
    await show(detail());

    expect(root.querySelector('h1')?.textContent).toContain('Acme s.r.o.');
    expect(input('supplierName').value).toBe('Acme s.r.o.');
    expect(input('totalAmount').value).toBe('121');
    expect(input('supplierName').readOnly).toBeFalse();
    expect(button('Submit for approval')).toBeDefined();
    expect(button('Cancel invoice')).toBeDefined();
    expect(button('Approve')).toBeUndefined();
    expect(button('Mark as paid')).toBeUndefined();
  });

  it('shows a paid invoice read-only with no actions', async () => {
    await show(detail({ status: 'Paid', paidOn: '2026-03-01', paymentReference: 'TX-9' }));

    expect(input('supplierName').readOnly).toBeTrue();
    expect(root.querySelector('.actions')?.querySelectorAll('button').length ?? 0).toBe(0);
    expect(root.textContent).toContain('TX-9');
  });

  it('offers approve and reject for an invoice waiting for approval', async () => {
    await show(detail({ status: 'PendingApproval' }));

    expect(button('Approve')).toBeDefined();
    expect(button('Reject')).toBeDefined();
    expect(button('Submit for approval')).toBeUndefined();
  });

  it('sends the version it read as If-Match and shows the result', async () => {
    await show(detail({ status: 'PendingApproval' }), 'v1');

    button('Approve')!.click();
    TestBed.tick();
    const request: TestRequest = http.expectOne(`/api/v1/invoices/${ID}/approve`);
    expect(request.request.method).toBe('POST');
    expect(request.request.headers.get('If-Match')).toBe('"v1"');
    request.flush(detail({ status: 'Approved' }), { status: 200, statusText: 'OK', ...etag('v2') });
    await settle();
    http.expectOne((r) => r.url === `/api/v1/invoices/${ID}/audit`).flush(emptyAudit);
    await settle();

    expect(root.textContent).toContain('Approved');
    expect(button('Mark as paid')).toBeDefined();
    expect(button('Approve')).toBeUndefined();
  });

  it('saves edited fields with PUT, If-Match and the full set of fields', async () => {
    await show(detail(), 'v7');

    const supplier = input('supplierName');
    supplier.value = 'Beta GmbH';
    supplier.dispatchEvent(new Event('input'));
    await settle();
    expect(button('Submit for approval')!.disabled).toBeTrue();

    button('Save')!.click();
    TestBed.tick();
    const request = http.expectOne(`/api/v1/invoices/${ID}/fields`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.headers.get('If-Match')).toBe('"v7"');
    expect(request.request.body).toEqual({
      supplierName: 'Beta GmbH',
      invoiceNumber: 'INV-1',
      invoiceDate: '2026-01-15',
      dueDate: '2026-02-15',
      currency: 'EUR',
      subtotal: 100,
      taxAmount: 21,
      totalAmount: 121,
      purchaseOrderReference: null,
    });
    request.flush(detail({}, { supplierName: 'Beta GmbH' }), { status: 200, statusText: 'OK', ...etag('v8') });
    await settle();
    http.expectOne((r) => r.url === `/api/v1/invoices/${ID}/audit`).flush(emptyAudit);
    await settle();

    expect(root.querySelector('h1')?.textContent).toContain('Beta GmbH');
    expect(button('Submit for approval')!.disabled).toBeFalse();
  });

  it('maps field errors from the server onto the fields by JSON Pointer', async () => {
    await show(detail());

    button('Submit for approval')!.click();
    TestBed.tick();
    http.expectOne(`/api/v1/invoices/${ID}/submit`).flush(
      {
        code: 'invoice.not_ready_for_submission',
        status: 422,
        traceId: 't-1',
        errors: [
          { pointer: '/dueDate', code: 'validation.required', params: { field: 'dueDate' } },
          { pointer: '/totalAmount', code: 'validation.total_mismatch', params: {} },
          { pointer: '', code: 'validation.failed', params: {} },
        ],
      },
      { status: 422, statusText: 'Unprocessable' },
    );
    await settle();

    const errors = Array.from(root.querySelectorAll('mat-error')).map((e) => e.textContent?.trim());
    expect(errors).toContain('This field is required.');
    expect(errors).toContain('The total must equal the net amount plus VAT.');
    // The error without a field is listed above the form instead of being lost.
    expect(root.querySelector('.banner.error')?.textContent).toContain('Some values are invalid');
  });

  it('offers to reload when someone else changed the invoice', async () => {
    await show(detail({ status: 'PendingApproval' }), 'v1');

    button('Approve')!.click();
    TestBed.tick();
    http.expectOne(`/api/v1/invoices/${ID}/approve`).flush(
      { code: 'concurrency.stale_version', status: 412 },
      { status: 412, statusText: 'Precondition Failed' },
    );
    await settle();

    const dialog = document.querySelector('mat-dialog-container') as HTMLElement;
    expect(dialog.textContent).toContain('The invoice has changed');
    const reload = Array.from(dialog.querySelectorAll('button')).find((b) => b.textContent?.includes('Reload invoice'));
    reload!.click();
    // The dialog reports its result once its closing animation has finished.
    await new Promise((resolve) => setTimeout(resolve, 500));
    await settle();

    // Reloading fetches the current version.
    http.expectOne(`/api/v1/invoices/${ID}`).flush(detail({ status: 'Approved' }), { status: 200, statusText: 'OK', ...etag('v2') });
    await settle();
    http.expectOne((r) => r.url === `/api/v1/invoices/${ID}/audit`).flush(emptyAudit);
    await settle();
    expect(root.textContent).toContain('Approved');
  });

  it('shows a not found message with the trace ID', async () => {
    await harness.navigateByUrl(`/invoices/${ID}`);
    TestBed.tick();
    http.expectOne(`/api/v1/invoices/${ID}`).flush(
      { code: 'invoice.not_found', status: 404, traceId: 'trace-404' },
      { status: 404, statusText: 'Not Found' },
    );
    await settle();
    root = harness.routeNativeElement as HTMLElement;

    expect(root.querySelector('.notice.error')?.textContent).toContain('Invoice not found');
    expect(root.querySelector('.notice.error')?.textContent).toContain('trace-404');
  });
});
