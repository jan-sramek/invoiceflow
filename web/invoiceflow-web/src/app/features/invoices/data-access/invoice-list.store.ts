import { inject } from '@angular/core';
import { ApiError, toApiError } from '@core/http';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import type { InvoiceSummaryDto } from '@shared/api';
import { EMPTY, catchError, pipe, switchMap, tap } from 'rxjs';
import { DEFAULT_LIST_QUERY, InvoiceListQuery } from '../util';
import { InvoicesApi } from './invoices.api';

interface InvoiceListState {
  query: InvoiceListQuery;
  items: readonly InvoiceSummaryDto[];
  totalCount: number;
  loading: boolean;
  /** The last load failed; the list shows a retry instead of an empty result. */
  error: ApiError | null;
}

const initialState: InvoiceListState = {
  query: DEFAULT_LIST_QUERY,
  items: [],
  totalCount: 0,
  loading: false,
  error: null,
};

/** State of the invoice list page. Provide it on the routed component; the query drives the loading. */
export const InvoiceListStore = signalStore(
  withState(initialState),
  withMethods((store, api = inject(InvoicesApi)) => {
    const load = rxMethod<InvoiceListQuery>(
      pipe(
        tap(() => patchState(store, { loading: true, error: null })),
        // A newer query cancels the request still in flight, so a slow answer can never overwrite a newer one.
        switchMap((query) =>
          api.list(query).pipe(
            tap((page) =>
              patchState(store, { items: page.items, totalCount: page.totalCount, loading: false }),
            ),
            catchError((error: unknown) => {
              patchState(store, { loading: false, error: toApiError(error), items: [], totalCount: 0 });
              return EMPTY;
            }),
          ),
        ),
      ),
    );

    return {
      setQuery(query: InvoiceListQuery): void {
        patchState(store, { query });
      },
      /** Loads the current query again, e.g. after an error. */
      reload(): void {
        load(store.query());
      },
      _load: load,
    };
  }),
  withHooks({
    onInit(store) {
      store._load(store.query);
    },
  }),
);

export type InvoiceListStoreType = InstanceType<typeof InvoiceListStore>;
