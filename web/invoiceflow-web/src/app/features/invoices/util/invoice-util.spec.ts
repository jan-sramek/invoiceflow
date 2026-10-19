import { INVOICE_STATUSES } from '@shared/api';
import { capabilitiesOf, isExtracting } from './invoice-actions';
import {
  DEFAULT_LIST_QUERY,
  activeFilterCount,
  fromRouteParams,
  sortParts,
  toApiParams,
  toRouteParams,
  toSort,
} from './invoice-list-query';

function params(entries: Record<string, string | string[]>) {
  return {
    get: (name: string) => {
      const value = entries[name];
      return Array.isArray(value) ? (value[0] ?? null) : (value ?? null);
    },
    getAll: (name: string) => {
      const value = entries[name];
      return value === undefined ? [] : Array.isArray(value) ? value : [value];
    },
  };
}

describe('capabilitiesOf', () => {
  it('matches the domain transitions for every status', () => {
    const expected: Record<string, string[]> = {
      Uploaded: ['canEnterManually', 'canCancel'],
      Processing: [],
      Extracted: ['canEditFields', 'canSubmit', 'canCancel'],
      NeedsReview: ['canEditFields', 'canSubmit', 'canCancel'],
      ExtractionFailed: ['canEnterManually', 'canRetryExtraction', 'canCancel'],
      PendingApproval: ['canApprove', 'canReject', 'canCancel'],
      Approved: ['canMarkPaid'],
      Rejected: ['canReopen', 'canCancel'],
      Paid: [],
      Cancelled: [],
    };

    for (const status of INVOICE_STATUSES) {
      const allowed = Object.entries(capabilitiesOf(status, 'None'))
        .filter(([, value]) => value)
        .map(([name]) => name)
        .sort();
      expect(allowed).withContext(status).toEqual([...expected[status]].sort());
    }
  });

  it('offers to clear a suspected duplicate only while the invoice is before approval', () => {
    expect(capabilitiesOf('NeedsReview', 'Suspected').canAcknowledgeDuplicate).toBeTrue();
    expect(capabilitiesOf('Uploaded', 'Suspected').canAcknowledgeDuplicate).toBeTrue();
    expect(capabilitiesOf('PendingApproval', 'Suspected').canAcknowledgeDuplicate).toBeFalse();
    expect(capabilitiesOf('NeedsReview', 'None').canAcknowledgeDuplicate).toBeFalse();
    expect(capabilitiesOf('NeedsReview', 'Cleared').canAcknowledgeDuplicate).toBeFalse();
  });

  it('knows when extraction is running', () => {
    expect(isExtracting('Uploaded')).toBeTrue();
    expect(isExtracting('Processing')).toBeTrue();
    expect(isExtracting('Extracted')).toBeFalse();
  });
});

describe('invoice list query', () => {
  it('sends only what is set to the API', () => {
    expect(toApiParams(DEFAULT_LIST_QUERY)).toEqual({ page: 1, pageSize: 25, sort: '-uploadedAt' });

    expect(
      toApiParams({
        ...DEFAULT_LIST_QUERY,
        status: ['Approved', 'Paid'],
        search: '  acme ',
        currency: 'eur',
        totalAmountFrom: 0,
        overdue: true,
        duplicateStatus: 'Suspected',
        dueDateTo: '2026-12-31',
      }),
    ).toEqual({
      page: 1,
      pageSize: 25,
      sort: '-uploadedAt',
      status: ['Approved', 'Paid'],
      search: 'acme',
      currency: 'EUR',
      totalAmountFrom: 0,
      overdue: true,
      duplicateStatus: 'Suspected',
      dueDateTo: '2026-12-31',
    });
  });

  it('leaves defaults out of the URL', () => {
    const route = toRouteParams(DEFAULT_LIST_QUERY);
    expect(Object.values(route).every((value) => value === null)).toBeTrue();
  });

  it('round-trips through the URL', () => {
    const query = {
      ...DEFAULT_LIST_QUERY,
      status: ['NeedsReview', 'Rejected'] as const,
      search: 'acme',
      invoiceDateFrom: '2026-01-01',
      totalAmountTo: 99.5,
      currency: 'CZK',
      overdue: true,
      duplicateStatus: 'Cleared' as const,
      sort: 'dueDate',
      page: 3,
      pageSize: 50,
    };
    const route = toRouteParams(query);
    const entries: Record<string, string | string[]> = {};
    for (const [key, value] of Object.entries(route)) {
      if (value !== null) entries[key] = value;
    }

    expect(fromRouteParams(params(entries))).toEqual(query);
  });

  it('falls back to defaults for malformed URL values', () => {
    const query = fromRouteParams(
      params({
        status: ['Approved', 'Bogus'],
        invoiceDateFrom: 'yesterday',
        totalAmountFrom: 'abc',
        duplicateStatus: 'Maybe',
        sort: '-nonsense',
        page: '-4',
        pageSize: '9999',
      }),
    );

    expect(query.status).toEqual(['Approved']);
    expect(query.invoiceDateFrom).toBe('');
    expect(query.totalAmountFrom).toBeNull();
    expect(query.duplicateStatus).toBeNull();
    expect(query.sort).toBe('-uploadedAt');
    expect(query.page).toBe(1);
    expect(query.pageSize).toBe(25);
  });

  it('counts the active filters but not search, sort or paging', () => {
    expect(activeFilterCount(DEFAULT_LIST_QUERY)).toBe(0);
    expect(
      activeFilterCount({
        ...DEFAULT_LIST_QUERY,
        search: 'x',
        page: 2,
        status: ['Paid'],
        dueDateFrom: '2026-01-01',
        overdue: true,
      }),
    ).toBe(3);
  });

  it('converts between sort strings and Material sort state', () => {
    expect(sortParts('-dueDate')).toEqual({ active: 'dueDate', direction: 'desc' });
    expect(sortParts('status')).toEqual({ active: 'status', direction: 'asc' });
    expect(toSort('dueDate', 'desc')).toBe('-dueDate');
    expect(toSort('dueDate', 'asc')).toBe('dueDate');
    expect(toSort('dueDate', '')).toBe('-uploadedAt');
  });
});
