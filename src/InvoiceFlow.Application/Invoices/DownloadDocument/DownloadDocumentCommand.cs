using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.DownloadDocument;

/// <summary>
/// A command rather than a query because every download is audited.
/// </summary>
public sealed record DownloadDocumentCommand(InvoiceId Id) : ICommand<DocumentContent>;

/// <summary>
/// The caller owns <see cref="Content"/> and must dispose it.
/// </summary>
public sealed record DocumentContent(Stream Content, string FileName, long SizeBytes);
