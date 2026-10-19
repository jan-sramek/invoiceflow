import { computed, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ApiError, ServerErrorCode, toApiError, type ApiFieldError } from '@core/http';
import { ErrorNotifier } from '@core/error-handling';
import { TranslocoService } from '@jsverse/transloco';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import type { InvoiceAuditEntryDto, InvoiceDetailDto, InvoiceFieldsInput } from '@shared/api';
import { EMPTY, Observable, catchError, exhaustMap, map, pipe, switchMap, tap, timer } from 'rxjs';
import { capabilitiesOf, isExtracting, saveBlob } from '../util';
import { InvoicesApi, Versioned } from './invoices.api';

const POLL_INTERVAL_MS = 3000;
const AUDIT_PAGE_SIZE = 50;

interface InvoiceDetailState {
  id: string | null;
  invoice: InvoiceDetailDto | null;
  etag: string | null;
  loading: boolean;
  /** Loading the invoice failed (not found, no access, unreachable). */
  loadError: ApiError | null;
  /** A write is in flight; the buttons wait so the same click cannot be sent twice. */
  busy: boolean;
  /** Field errors of the last write, for the page to put on the form. Replaced on every write. */
  fieldErrors: readonly ApiFieldError[];
  downloading: boolean;
  audit: readonly InvoiceAuditEntryDto[];
  auditTotal: number;
  auditPage: number;
  auditLoading: boolean;
  auditError: ApiError | null;
}

const initialState: InvoiceDetailState = {
  id: null,
  invoice: null,
  etag: null,
  loading: false,
  loadError: null,
  busy: false,
  fieldErrors: [],
  downloading: false,
  audit: [],
  auditTotal: 0,
  auditPage: 0,
  auditLoading: false,
  auditError: null,
};

/** A change to the invoice, given the version it must be based on. `success` is the translation key of the confirmation. */
interface WriteCommand {
  readonly send: (etag: string) => Observable<Versioned<InvoiceDetailDto>>;
  readonly success: string;
}

/** State of the invoice detail page. Provide it on the routed component. */
export const InvoiceDetailStore = signalStore(
  withState(initialState),
  withComputed((store) => {
    const status = computed(() => store.invoice()?.status ?? null);
    return {
      status,
      capabilities: computed(() => {
        const invoice = store.invoice();
        return invoice ? capabilitiesOf(invoice.status, invoice.duplicate?.status ?? 'None') : null;
      }),
      extracting: computed(() => {
        const current = status();
        return current !== null && isExtracting(current);
      }),
      hasMoreAudit: computed(() => store.audit().length < store.auditTotal()),
    };
  }),
  withMethods(
    (
      store,
      api = inject(InvoicesApi),
      notifier = inject(ErrorNotifier),
      snackBar = inject(MatSnackBar),
      transloco = inject(TranslocoService),
    ) => {
      function applyInvoice(result: Versioned<InvoiceDetailDto>): void {
        patchState(store, { invoice: result.value, etag: result.etag });
      }

      const loadAuditPage = rxMethod<{ id: string; page: number }>(
        pipe(
          tap(() => patchState(store, { auditLoading: true, auditError: null })),
          switchMap(({ id, page }) =>
            api.audit(id, page, AUDIT_PAGE_SIZE).pipe(
              tap((result) =>
                patchState(store, {
                  audit: page === 1 ? result.items : [...store.audit(), ...result.items],
                  auditTotal: result.totalCount,
                  auditPage: page,
                  auditLoading: false,
                }),
              ),
              catchError((error: unknown) => {
                patchState(store, { auditLoading: false, auditError: toApiError(error) });
                return EMPTY;
              }),
            ),
          ),
        ),
      );

      const load = rxMethod<string>(
        pipe(
          tap((id) =>
            patchState(store, { ...initialState, id, loading: true }),
          ),
          switchMap((id) =>
            api.get(id).pipe(
              tap((result) => {
                patchState(store, { loading: false });
                applyInvoice(result);
                loadAuditPage({ id, page: 1 });
              }),
              catchError((error: unknown) => {
                patchState(store, { loading: false, loadError: toApiError(error) });
                return EMPTY;
              }),
            ),
          ),
        ),
      );

      /** Re-reads the invoice quietly while extraction runs; stops by itself once it is done. */
      const poll = rxMethod<boolean>(
        pipe(
          switchMap((active) =>
            active
              ? timer(POLL_INTERVAL_MS, POLL_INTERVAL_MS).pipe(
                  exhaustMap(() => {
                    const id = store.id();
                    return id
                      ? api.get(id).pipe(
                          tap((result) => {
                            const changed = result.etag !== store.etag();
                            applyInvoice(result);
                            if (changed) {
                              loadAuditPage({ id, page: 1 });
                            }
                          }),
                          catchError(() => EMPTY),
                        )
                      : EMPTY;
                  }),
                )
              : EMPTY,
          ),
        ),
      );

      const write = rxMethod<WriteCommand>(
        pipe(
          // One write at a time: a second click while one is in flight is dropped, not queued.
          exhaustMap((command) => {
            const etag = store.etag();
            if (etag === null) {
              return EMPTY;
            }
            patchState(store, { busy: true, fieldErrors: [] });
            return command.send(etag).pipe(
              tap((result) => {
                applyInvoice(result);
                patchState(store, { busy: false });
                snackBar.open(transloco.translate(command.success), undefined, { duration: 3500 });
                const id = store.id();
                if (id) {
                  loadAuditPage({ id, page: 1 });
                }
              }),
              catchError((error: unknown) => {
                const apiError = toApiError(error);
                patchState(store, { busy: false, fieldErrors: apiError.errors });
                if (apiError.is(ServerErrorCode.StaleVersion)) {
                  notifier.confirmReloadAfterStaleVersion().subscribe((reload) => {
                    if (reload && store.id()) {
                      load(store.id() as string);
                    }
                  });
                } else if (apiError.hasFieldErrors) {
                  // The interceptor leaves field errors to the form; say that something was refused.
                  notifier.show(apiError);
                }
                return EMPTY;
              }),
            );
          }),
        ),
      );

      const id = (): string => store.id() as string;

      return {
        load,
        poll,
        loadMoreAudit(): void {
          const current = store.id();
          if (current && !store.auditLoading() && store.hasMoreAudit()) {
            loadAuditPage({ id: current, page: store.auditPage() + 1 });
          }
        },
        reload(): void {
          const current = store.id();
          if (current) {
            load(current);
          }
        },
        saveFields(fields: InvoiceFieldsInput): void {
          write({
            send: (etag) => api.updateFields(id(), etag, fields),
            success: 'invoices.detail.saved',
          });
        },
        enterManually(): void {
          write({ send: (etag) => api.act(id(), 'enter-manually', etag), success: 'invoices.detail.enteredManually' });
        },
        retryExtraction(): void {
          write({
            send: (etag) => api.act(id(), 'retry-extraction', etag),
            success: 'invoices.detail.extractionRetried',
          });
        },
        submit(): void {
          write({ send: (etag) => api.act(id(), 'submit', etag), success: 'invoices.detail.submitted' });
        },
        approve(): void {
          write({ send: (etag) => api.act(id(), 'approve', etag), success: 'invoices.detail.approved' });
        },
        reopen(): void {
          write({ send: (etag) => api.act(id(), 'reopen', etag), success: 'invoices.detail.reopened' });
        },
        reject(reason: string): void {
          write({ send: (etag) => api.reject(id(), etag, reason), success: 'invoices.detail.rejected' });
        },
        cancel(reason: string): void {
          write({ send: (etag) => api.cancel(id(), etag, reason), success: 'invoices.detail.cancelled' });
        },
        acknowledgeNotDuplicate(reason: string): void {
          write({
            send: (etag) => api.acknowledgeNotDuplicate(id(), etag, reason),
            success: 'invoices.detail.duplicateCleared',
          });
        },
        markPaid(paidOn: string, paymentReference: string): void {
          write({
            send: (etag) => api.markPaid(id(), etag, paidOn, paymentReference),
            success: 'invoices.detail.paid',
          });
        },
        downloadDocument(): void {
          const current = store.invoice();
          if (!current || store.downloading()) {
            return;
          }
          patchState(store, { downloading: true });
          api
            .downloadDocument(current.id)
            .pipe(map((blob) => saveBlob(blob, current.document.originalFileName)))
            .subscribe({
              next: () => patchState(store, { downloading: false }),
              // The error interceptor has already shown it.
              error: () => patchState(store, { downloading: false }),
            });
        },
      };
    },
  ),
);

export type InvoiceDetailStoreType = InstanceType<typeof InvoiceDetailStore>;
