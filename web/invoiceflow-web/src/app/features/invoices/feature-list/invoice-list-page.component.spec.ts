import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { provideTransloco, TranslocoLoader } from '@jsverse/transloco';
import { of } from 'rxjs';
import en from '../../../../../public/i18n/en.json';
import { InvoiceListPageComponent } from './invoice-list-page.component';

class EnglishLoader implements TranslocoLoader {
  getTranslation() {
    return of(en);
  }
}

const summary = (over: object = {}) => ({
  id: '11111111-1111-1111-1111-111111111111',
  status: 'Approved',
  supplierName: 'Acme s.r.o.',
  invoiceNumber: 'INV-1',
  invoiceDate: '2026-01-15',
  dueDate: '2026-02-15',
  currency: 'EUR',
  totalAmount: 1234.5,
  uploadedAt: '2026-01-16T10:00:00Z',
  isOverdue: true,
  duplicateStatus: 'Suspected',
  ...over,
});

describe('InvoiceListPageComponent', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideRouter([{ path: 'invoices', component: InvoiceListPageComponent }], withComponentInputBinding()),
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

  async function open(url: string) {
    await harness.navigateByUrl(url);
    TestBed.tick();
    return http.expectOne((r) => r.url === '/api/v1/invoices');
  }

  it('asks for the page the URL describes, with repeated status parameters', async () => {
    const request = await open('/invoices?status=Approved&status=Paid&search=acme&page=2&pageSize=50&sort=dueDate');

    expect(request.request.params.getAll('status')).toEqual(['Approved', 'Paid']);
    expect(request.request.params.get('search')).toBe('acme');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('50');
    expect(request.request.params.get('sort')).toBe('dueDate');
    request.flush({ items: [], page: 2, pageSize: 50, totalCount: 0 });
  });

  it('lists invoices with status, flags, formatted amounts and a link to the detail', async () => {
    const request = await open('/invoices');
    request.flush({ items: [summary()], page: 1, pageSize: 25, totalCount: 1 });
    await harness.fixture.whenStable();
    harness.detectChanges();

    const root = harness.routeNativeElement as HTMLElement;
    const row = root.querySelector('tr.row') as HTMLElement;
    expect(row.textContent).toContain('Acme s.r.o.');
    expect(row.textContent).toContain('INV-1');
    expect(row.textContent).toContain('Approved');
    expect(row.textContent).toContain('Overdue');
    expect(row.textContent).toContain('Possible duplicate');
    expect(row.textContent).toContain('1,234.50');
    expect(row.querySelector('a.supplier')?.getAttribute('href')).toBe('/invoices/11111111-1111-1111-1111-111111111111');
  });

  it('shows an empty state with a way to clear the filters when nothing matches', async () => {
    const request = await open('/invoices?search=nothing');
    request.flush({ items: [], page: 1, pageSize: 25, totalCount: 0 });
    await harness.fixture.whenStable();
    harness.detectChanges();

    const text = (harness.routeNativeElement as HTMLElement).textContent;
    expect(text).toContain('No invoices match');
  });

  it('shows the failure with its trace ID and loads again on retry', async () => {
    const request = await open('/invoices');
    request.flush(
      { code: 'server.unexpected', status: 500, traceId: 'trace-123' },
      { status: 500, statusText: 'Server Error' },
    );
    await harness.fixture.whenStable();
    harness.detectChanges();

    const root = harness.routeNativeElement as HTMLElement;
    expect(root.querySelector('.notice.error')?.textContent).toContain('trace-123');

    (root.querySelector('.notice button') as HTMLButtonElement).click();
    TestBed.tick();
    http.expectOne((r) => r.url === '/api/v1/invoices').flush({ items: [], page: 1, pageSize: 25, totalCount: 0 });
  });

  it('puts a changed filter in the URL and loads page 1 again', async () => {
    (await open('/invoices?page=3')).flush({ items: [], page: 3, pageSize: 25, totalCount: 0 });
    await harness.fixture.whenStable();
    harness.detectChanges();

    const input = (harness.routeNativeElement as HTMLElement).querySelector('input[type=search]') as HTMLInputElement;
    input.value = 'acme';
    input.dispatchEvent(new Event('input'));
    await new Promise((resolve) => setTimeout(resolve, 400));
    TestBed.tick();
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/invoices?search=acme');
    const request = http.expectOne((r) => r.url === '/api/v1/invoices');
    expect(request.request.params.get('search')).toBe('acme');
    expect(request.request.params.get('page')).toBe('1');
    request.flush({ items: [], page: 1, pageSize: 25, totalCount: 0 });
  });
});
