import {
  HttpClient,
  HttpContext,
  HttpEvent,
  HttpEventType,
  HttpHeaders,
  HttpParams,
  HttpResponse,
} from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AppConfigService } from '@core/config';
import { IDEMPOTENCY_KEY_HEADER, SUPPRESS_ERROR_NOTIFICATION } from '@core/http';
import type {
  InvoiceAuditEntryDto,
  InvoiceDetailDto,
  InvoiceFieldsInput,
  InvoiceSummaryDto,
  PagedInvoiceAuditEntryDto,
  PagedInvoiceSummaryDto,
} from '@shared/api';
import { Observable, filter, map } from 'rxjs';
import { InvoiceListQuery, toApiParams } from '../util';

/** A resource with the `ETag` it was read with; send the tag back as `If-Match` on the next write. */
export interface Versioned<T> {
  readonly value: T;
  readonly etag: string;
}

export type UploadEvent =
  | { readonly type: 'progress'; readonly percent: number }
  | { readonly type: 'done'; readonly invoice: Versioned<InvoiceDetailDto> };

export type InvoiceAction = 'enter-manually' | 'retry-extraction' | 'submit' | 'approve' | 'reopen';

export interface InvoicePage<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

/** Every call the invoice screens make. Errors arrive as `ApiError` (see core/http). */
@Injectable({ providedIn: 'root' })
export class InvoicesApi {
  private readonly http = inject(HttpClient);
  private readonly config = inject(AppConfigService);

  private url(path = ''): string {
    return `${this.config.apiBaseUrl}/v1/invoices${path}`;
  }

  list(query: InvoiceListQuery): Observable<InvoicePage<InvoiceSummaryDto>> {
    const { status, ...rest } = toApiParams(query);
    let params = new HttpParams();
    for (const [name, value] of Object.entries(rest)) {
      params = params.set(name, String(value));
    }
    // Repeated, not comma separated: the API binds `status=A&status=B`.
    for (const value of status ?? []) {
      params = params.append('status', value);
    }
    return this.http.get<PagedInvoiceSummaryDto>(this.url(), { params });
  }

  get(id: string): Observable<Versioned<InvoiceDetailDto>> {
    return this.http
      .get<InvoiceDetailDto>(this.url(`/${id}`), { observe: 'response' })
      .pipe(map(toVersioned));
  }

  audit(id: string, page: number, pageSize = 50): Observable<InvoicePage<InvoiceAuditEntryDto>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedInvoiceAuditEntryDto>(this.url(`/${id}/audit`), { params });
  }

  downloadDocument(id: string): Observable<Blob> {
    return this.http.get(this.url(`/${id}/document`), { responseType: 'blob' });
  }

  /**
   * Uploads one PDF. The same `idempotencyKey` for the same file makes a retry safe: the server returns
   * the first invoice instead of creating another. Errors are left to the caller (the upload list shows them per file).
   */
  upload(file: File, idempotencyKey: string): Observable<UploadEvent> {
    const body = new FormData();
    body.append('file', file, file.name);

    return this.http
      .post<InvoiceDetailDto>(this.url(), body, {
        headers: new HttpHeaders({ [IDEMPOTENCY_KEY_HEADER]: idempotencyKey }),
        context: new HttpContext().set(SUPPRESS_ERROR_NOTIFICATION, true),
        observe: 'events',
        reportProgress: true,
      })
      .pipe(
        filter(
          (event: HttpEvent<InvoiceDetailDto>) =>
            event.type === HttpEventType.UploadProgress || event.type === HttpEventType.Response,
        ),
        map((event): UploadEvent => {
          if (event.type === HttpEventType.Response) {
            return { type: 'done', invoice: toVersioned(event as HttpResponse<InvoiceDetailDto>) };
          }
          const progress = event as { loaded: number; total?: number };
          return {
            type: 'progress',
            percent: progress.total ? Math.round((progress.loaded / progress.total) * 100) : 0,
          };
        }),
      );
  }

  updateFields(id: string, etag: string, fields: InvoiceFieldsInput): Observable<Versioned<InvoiceDetailDto>> {
    return this.write('put', this.url(`/${id}/fields`), etag, fields);
  }

  /** Actions without a body: enter-manually, submit, approve, reopen. */
  act(id: string, action: InvoiceAction, etag: string): Observable<Versioned<InvoiceDetailDto>> {
    return this.write('post', this.url(`/${id}/${action}`), etag, null);
  }

  acknowledgeNotDuplicate(id: string, etag: string, reason: string): Observable<Versioned<InvoiceDetailDto>> {
    return this.write('post', this.url(`/${id}/acknowledge-not-duplicate`), etag, { reason });
  }

  reject(id: string, etag: string, reason: string): Observable<Versioned<InvoiceDetailDto>> {
    return this.write('post', this.url(`/${id}/reject`), etag, { reason });
  }

  cancel(id: string, etag: string, reason: string): Observable<Versioned<InvoiceDetailDto>> {
    return this.write('post', this.url(`/${id}/cancel`), etag, { reason });
  }

  markPaid(
    id: string,
    etag: string,
    paidOn: string,
    paymentReference: string,
  ): Observable<Versioned<InvoiceDetailDto>> {
    return this.write('post', this.url(`/${id}/mark-paid`), etag, { paidOn, paymentReference });
  }

  private write(
    method: 'put' | 'post',
    url: string,
    etag: string,
    body: unknown,
  ): Observable<Versioned<InvoiceDetailDto>> {
    return this.http
      .request<InvoiceDetailDto>(method.toUpperCase(), url, {
        body,
        headers: new HttpHeaders({ 'If-Match': etag }),
        observe: 'response',
      })
      .pipe(map(toVersioned));
  }
}

function toVersioned(response: HttpResponse<InvoiceDetailDto>): Versioned<InvoiceDetailDto> {
  return { value: response.body as InvoiceDetailDto, etag: response.headers.get('ETag') ?? '' };
}
