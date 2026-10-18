using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.DownloadDocument;

public sealed record DownloadInvoiceDocumentCommand(InvoiceId Id) : ICommand<InvoiceDocumentAccess>;

public sealed record InvoiceDocumentAccess(string StorageKey, string FileName, string ContentType, long SizeBytes);
