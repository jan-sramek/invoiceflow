import type { DuplicateStatus, InvoiceStatus } from '@shared/api';

/** What the user may do with an invoice, from its status. Mirrors the transitions in the domain; the server has the last word. */
export interface InvoiceCapabilities {
  readonly canEditFields: boolean;
  readonly canEnterManually: boolean;
  readonly canRetryExtraction: boolean;
  readonly canSubmit: boolean;
  readonly canApprove: boolean;
  readonly canReject: boolean;
  readonly canReopen: boolean;
  readonly canMarkPaid: boolean;
  readonly canCancel: boolean;
  readonly canAcknowledgeDuplicate: boolean;
}

const EDITABLE: readonly InvoiceStatus[] = ['Extracted', 'NeedsReview'];
const CANCELLABLE: readonly InvoiceStatus[] = [
  'Uploaded',
  'ExtractionFailed',
  'Extracted',
  'NeedsReview',
  'PendingApproval',
  'Rejected',
];
const DUPLICATE_RESOLVABLE: readonly InvoiceStatus[] = [
  'Uploaded',
  'Processing',
  'Extracted',
  'NeedsReview',
  'ExtractionFailed',
];

export function capabilitiesOf(status: InvoiceStatus, duplicate: DuplicateStatus): InvoiceCapabilities {
  return {
    canEditFields: EDITABLE.includes(status),
    canEnterManually: status === 'Uploaded' || status === 'ExtractionFailed',
    canRetryExtraction: status === 'ExtractionFailed',
    // Offered even for a suspected duplicate: the server answers invoice.unresolved_duplicate and the page explains.
    canSubmit: EDITABLE.includes(status),
    canApprove: status === 'PendingApproval',
    canReject: status === 'PendingApproval',
    canReopen: status === 'Rejected',
    canMarkPaid: status === 'Approved',
    canCancel: CANCELLABLE.includes(status),
    canAcknowledgeDuplicate: duplicate === 'Suspected' && DUPLICATE_RESOLVABLE.includes(status),
  };
}

/** Extraction is under way, so the invoice will change without anyone touching it. */
export function isExtracting(status: InvoiceStatus): boolean {
  return status === 'Uploaded' || status === 'Processing';
}

export const FINAL_STATUSES: readonly InvoiceStatus[] = ['Paid', 'Cancelled'];
