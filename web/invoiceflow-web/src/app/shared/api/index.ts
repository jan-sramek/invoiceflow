import type { components, operations } from './schema';

export type { components, operations, paths } from './schema';

type Schemas = components['schemas'];

export type InvoiceStatus = Schemas['InvoiceStatus'];
export type DuplicateStatus = Schemas['DuplicateStatus'];
export type ActorDto = Schemas['ActorDto'];
export type InvoiceDetailDto = Schemas['InvoiceDetailDto'];
export type InvoiceDocumentDto = Schemas['InvoiceDocumentDto'];
export type InvoiceDuplicateDto = Schemas['InvoiceDuplicateDto'];
export type InvoiceFieldsDto = Schemas['InvoiceFieldsDto'];
export type InvoiceFieldStatusesDto = Schemas['InvoiceFieldStatusesDto'];
export type FieldStatus = Schemas['FieldStatus'];
export type InvoiceFieldsInput = Schemas['InvoiceFieldsInput'];
export type InvoiceSummaryDto = Schemas['InvoiceSummaryDto'];
export type PagedInvoiceSummaryDto = Schemas['PagedInvoiceSummaryDto'];
export type InvoiceAuditEntryDto = Schemas['InvoiceAuditEntryDto'];
export type AuditChangeDto = Schemas['AuditChangeDto'];
export type PagedInvoiceAuditEntryDto = Schemas['PagedInvoiceAuditEntryDto'];
export type ReasonRequest = Schemas['ReasonRequest'];
export type PaymentRequest = Schemas['PaymentRequest'];
export type ProblemDetailsDto = Schemas['ProblemDetails'];
export type FieldErrorDto = Schemas['FieldError'];

export type ListInvoicesQueryParams = NonNullable<operations['listInvoices']['parameters']['query']>;

export const INVOICE_STATUSES = [
  'Uploaded',
  'Processing',
  'Extracted',
  'NeedsReview',
  'ExtractionFailed',
  'PendingApproval',
  'Approved',
  'Rejected',
  'Paid',
  'Cancelled',
] as const satisfies readonly InvoiceStatus[];

export const DUPLICATE_STATUSES = [
  'None',
  'Suspected',
  'Cleared',
] as const satisfies readonly DuplicateStatus[];
