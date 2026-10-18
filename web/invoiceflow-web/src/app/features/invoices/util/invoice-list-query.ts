import {
  DUPLICATE_STATUSES,
  INVOICE_STATUSES,
  type DuplicateStatus,
  type InvoiceStatus,
  type ListInvoicesQueryParams,
} from '@shared/api';

/** Everything the invoice list can be narrowed or ordered by. Empty strings and `null` mean "no filter". */
export interface InvoiceListQuery {
  readonly status: readonly InvoiceStatus[];
  readonly search: string;
  readonly invoiceDateFrom: string;
  readonly invoiceDateTo: string;
  readonly dueDateFrom: string;
  readonly dueDateTo: string;
  readonly totalAmountFrom: number | null;
  readonly totalAmountTo: number | null;
  readonly currency: string;
  readonly overdue: boolean;
  readonly duplicateStatus: DuplicateStatus | null;
  readonly sort: string;
  readonly page: number;
  readonly pageSize: number;
}

export const DEFAULT_SORT = '-uploadedAt';
export const DEFAULT_PAGE_SIZE = 25;
export const MAX_PAGE_SIZE = 100;
export const PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const;
export const SORT_FIELDS = [
  'uploadedAt',
  'invoiceDate',
  'dueDate',
  'totalAmount',
  'status',
  'supplierName',
] as const;

export const DEFAULT_LIST_QUERY: InvoiceListQuery = {
  status: [],
  search: '',
  invoiceDateFrom: '',
  invoiceDateTo: '',
  dueDateFrom: '',
  dueDateTo: '',
  totalAmountFrom: null,
  totalAmountTo: null,
  currency: '',
  overdue: false,
  duplicateStatus: null,
  sort: DEFAULT_SORT,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
};

/** Query parameters for the API: only what is set, amounts as numbers, currency in upper case. */
export function toApiParams(query: InvoiceListQuery): ListInvoicesQueryParams {
  const params: ListInvoicesQueryParams = { page: query.page, pageSize: query.pageSize, sort: query.sort };
  if (query.status.length > 0) params.status = [...query.status];
  if (query.search.trim()) params.search = query.search.trim();
  if (query.invoiceDateFrom) params.invoiceDateFrom = query.invoiceDateFrom;
  if (query.invoiceDateTo) params.invoiceDateTo = query.invoiceDateTo;
  if (query.dueDateFrom) params.dueDateFrom = query.dueDateFrom;
  if (query.dueDateTo) params.dueDateTo = query.dueDateTo;
  if (query.totalAmountFrom !== null) params.totalAmountFrom = query.totalAmountFrom;
  if (query.totalAmountTo !== null) params.totalAmountTo = query.totalAmountTo;
  if (query.currency.trim()) params.currency = query.currency.trim().toUpperCase();
  if (query.overdue) params.overdue = true;
  if (query.duplicateStatus) params.duplicateStatus = query.duplicateStatus;
  return params;
}

/** The minimal reading side of `ParamMap`, so the functions below do not depend on the router. */
export interface ParamReader {
  get(name: string): string | null;
  getAll(name: string): string[];
}

/** Router query parameters for a query; defaults are left out so the URL stays short and shareable. */
export function toRouteParams(query: InvoiceListQuery): Record<string, string | string[] | null> {
  const d = DEFAULT_LIST_QUERY;
  return {
    status: query.status.length > 0 ? [...query.status] : null,
    search: query.search.trim() || null,
    invoiceDateFrom: query.invoiceDateFrom || null,
    invoiceDateTo: query.invoiceDateTo || null,
    dueDateFrom: query.dueDateFrom || null,
    dueDateTo: query.dueDateTo || null,
    totalAmountFrom: query.totalAmountFrom === null ? null : String(query.totalAmountFrom),
    totalAmountTo: query.totalAmountTo === null ? null : String(query.totalAmountTo),
    currency: query.currency.trim().toUpperCase() || null,
    overdue: query.overdue ? 'true' : null,
    duplicateStatus: query.duplicateStatus,
    sort: query.sort === d.sort ? null : query.sort,
    page: query.page === d.page ? null : String(query.page),
    pageSize: query.pageSize === d.pageSize ? null : String(query.pageSize),
  };
}

/** Reads a query from the URL. Unknown or malformed values fall back to the defaults instead of failing. */
export function fromRouteParams(params: ParamReader): InvoiceListQuery {
  const duplicate = params.get('duplicateStatus');
  const sort = params.get('sort') ?? DEFAULT_SORT;
  return {
    status: params.getAll('status').filter(isInvoiceStatus),
    search: params.get('search') ?? '',
    invoiceDateFrom: isoDate(params.get('invoiceDateFrom')),
    invoiceDateTo: isoDate(params.get('invoiceDateTo')),
    dueDateFrom: isoDate(params.get('dueDateFrom')),
    dueDateTo: isoDate(params.get('dueDateTo')),
    totalAmountFrom: amount(params.get('totalAmountFrom')),
    totalAmountTo: amount(params.get('totalAmountTo')),
    currency: (params.get('currency') ?? '').toUpperCase(),
    overdue: params.get('overdue') === 'true',
    duplicateStatus: (DUPLICATE_STATUSES as readonly string[]).includes(duplicate ?? '')
      ? (duplicate as DuplicateStatus)
      : null,
    sort: isSort(sort) ? sort : DEFAULT_SORT,
    page: integer(params.get('page'), 1, 10_000, 1),
    pageSize: integer(params.get('pageSize'), 1, MAX_PAGE_SIZE, DEFAULT_PAGE_SIZE),
  };
}

/** How many filters (not search, sort or paging) narrow the list; shown on the "more filters" toggle. */
export function activeFilterCount(query: InvoiceListQuery): number {
  return [
    query.status.length > 0,
    query.invoiceDateFrom || query.invoiceDateTo,
    query.dueDateFrom || query.dueDateTo,
    query.totalAmountFrom !== null || query.totalAmountTo !== null,
    query.currency,
    query.overdue,
    query.duplicateStatus,
  ].filter(Boolean).length;
}

export function sortParts(sort: string): { active: string; direction: 'asc' | 'desc' } {
  return sort.startsWith('-')
    ? { active: sort.slice(1), direction: 'desc' }
    : { active: sort, direction: 'asc' };
}

export function toSort(active: string, direction: 'asc' | 'desc' | ''): string {
  if (!active || !direction) {
    return DEFAULT_SORT;
  }
  return direction === 'desc' ? `-${active}` : active;
}

function isInvoiceStatus(value: string): value is InvoiceStatus {
  return (INVOICE_STATUSES as readonly string[]).includes(value);
}

function isSort(value: string): boolean {
  return (SORT_FIELDS as readonly string[]).includes(value.replace(/^-/, ''));
}

function isoDate(value: string | null): string {
  return value !== null && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : '';
}

function amount(value: string | null): number | null {
  if (value === null || value.trim() === '') {
    return null;
  }
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function integer(value: string | null, min: number, max: number, fallback: number): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= min && parsed <= max ? parsed : fallback;
}
