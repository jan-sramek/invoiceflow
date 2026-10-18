import { computed, inject } from '@angular/core';
import { ApiError, toApiError } from '@core/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import type { InvoiceDetailDto } from '@shared/api';
import { newIdempotencyKey } from '@shared/util';
import { EMPTY, catchError, mergeMap, pipe, tap } from 'rxjs';
import { InvoicesApi } from './invoices.api';

/** Files uploaded at the same time. Batch upload is several parallel calls of the single-file endpoint. */
export const MAX_PARALLEL_UPLOADS = 3;
/** Same limit as the server (`InvoiceDocument.MaxSizeBytes`); checked here only to spare a pointless upload. */
export const MAX_UPLOAD_BYTES = 10 * 1024 * 1024;

export type UploadItemState = 'queued' | 'uploading' | 'done' | 'failed';

export interface UploadItem {
  readonly id: string;
  readonly file: File;
  /** One key per file; reused when the same file is sent again after a lost connection. */
  readonly idempotencyKey: string;
  readonly state: UploadItemState;
  readonly percent: number;
  readonly invoice: InvoiceDetailDto | null;
  readonly error: ApiError | null;
}

interface UploadState {
  items: readonly UploadItem[];
}

export function isPdf(file: File): boolean {
  return file.type === 'application/pdf' || (file.type === '' && /\.pdf$/i.test(file.name));
}

/** Rejects what the server would reject anyway, with the same error codes so the same texts apply. */
export function validateFile(file: File): ApiError | null {
  if (!isPdf(file) || file.size === 0) {
    return new ApiError({ kind: 'problem', status: 415, code: 'upload.not_pdf' });
  }
  if (file.size > MAX_UPLOAD_BYTES) {
    return new ApiError({
      kind: 'problem',
      status: 413,
      code: 'upload.too_large',
      params: { maxBytes: MAX_UPLOAD_BYTES },
    });
  }
  return null;
}

/**
 * A failure where the same file with the same key would fail the same way, so a retry needs a new key.
 * After a lost connection or a 5xx the key is kept: the server may have stored the file already,
 * and the key makes it return that invoice instead of creating a second one.
 */
export function needsNewKey(error: ApiError): boolean {
  return error.status >= 400 && error.status < 500 && error.status !== 408 && error.status !== 429;
}

let nextItemId = 0;

/** State of the upload page. Provide it on the routed component. */
export const InvoiceUploadStore = signalStore(
  withState<UploadState>({ items: [] }),
  withComputed((store) => ({
    uploading: computed(() => store.items().some((i) => i.state === 'queued' || i.state === 'uploading')),
    doneCount: computed(() => store.items().filter((i) => i.state === 'done').length),
    failedCount: computed(() => store.items().filter((i) => i.state === 'failed').length),
    activeCount: computed(
      () => store.items().filter((i) => i.state === 'queued' || i.state === 'uploading').length,
    ),
  })),
  withMethods((store, api = inject(InvoicesApi)) => {
    function update(id: string, change: Partial<UploadItem>): void {
      patchState(store, {
        items: store.items().map((item) => (item.id === id ? { ...item, ...change } : item)),
      });
    }

    // mergeMap with a limit keeps at most three requests running; the rest wait in the queue.
    const send = rxMethod<string>(
      pipe(
        mergeMap((id) => {
          const item = store.items().find((candidate) => candidate.id === id);
          if (!item || item.state !== 'queued') {
            return EMPTY;
          }
          update(id, { state: 'uploading', percent: 0, error: null });
          return api.upload(item.file, item.idempotencyKey).pipe(
            tap((event) => {
              if (event.type === 'progress') {
                update(id, { percent: event.percent });
              }
            }),
            tap((event) => {
              if (event.type === 'done') {
                update(id, { state: 'done', percent: 100, invoice: event.invoice.value });
              }
            }),
            catchError((error: unknown) => {
              update(id, { state: 'failed', error: toApiError(error) });
              return EMPTY;
            }),
          );
        }, MAX_PARALLEL_UPLOADS),
      ),
    );

    function retry(id: string): void {
      const item = store.items().find((candidate) => candidate.id === id);
      if (!item || item.state !== 'failed' || validateFile(item.file)) {
        return;
      }
      const key = item.error && needsNewKey(item.error) ? newIdempotencyKey() : item.idempotencyKey;
      update(id, { state: 'queued', idempotencyKey: key, error: null, percent: 0 });
      send(id);
    }

    return {
      /** Adds files and starts uploading them. Files the server would refuse are listed as failed without being sent. */
      addFiles(files: readonly File[]): void {
        const added: UploadItem[] = files.map((file) => {
          const error = validateFile(file);
          return {
            id: `upload-${++nextItemId}`,
            file,
            idempotencyKey: newIdempotencyKey(),
            state: error ? 'failed' : 'queued',
            percent: 0,
            invoice: null,
            error,
          };
        });
        patchState(store, { items: [...store.items(), ...added] });
        for (const item of added) {
          if (item.state === 'queued') {
            send(item.id);
          }
        }
      },

      retry,

      retryFailed(): void {
        for (const item of store.items()) {
          if (item.state === 'failed') {
            retry(item.id);
          }
        }
      },

      /** Removes a file that is not being sent. */
      remove(id: string): void {
        patchState(store, {
          items: store.items().filter((item) => item.id !== id || item.state === 'uploading'),
        });
      },

      clearFinished(): void {
        patchState(store, {
          items: store.items().filter((item) => item.state === 'queued' || item.state === 'uploading'),
        });
      },
    };
  }),
);

export type InvoiceUploadStoreType = InstanceType<typeof InvoiceUploadStore>;
