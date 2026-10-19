namespace InvoiceFlow.Infrastructure.Messaging;

internal sealed record InvoiceUploadedMessage(Guid TenantId, Guid InvoiceId);
