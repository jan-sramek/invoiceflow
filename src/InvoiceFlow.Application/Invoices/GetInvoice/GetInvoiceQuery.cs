using InvoiceFlow.Application.Abstractions.Messaging;
using InvoiceFlow.Application.Common;
using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Application.Invoices.GetInvoice;

public sealed record GetInvoiceQuery(InvoiceId Id) : IQuery<Versioned<InvoiceDetailDto>>;
