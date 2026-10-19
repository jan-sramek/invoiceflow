namespace InvoiceFlow.Infrastructure.Messaging;

internal static class InvoiceFlowTopology
{
    public const string EventsExchange = "invoiceflow.events";
    public const string ProcessingQueue = "invoice-processing";
    public const string UploadedRoutingKey = "invoice.uploaded";
}
