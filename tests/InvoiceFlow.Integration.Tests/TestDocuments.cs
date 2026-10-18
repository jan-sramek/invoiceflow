using InvoiceFlow.Domain.Invoices;

namespace InvoiceFlow.Integration.Tests;

internal static class TestDocuments
{
    /// <summary>
    /// Metadata of a document that was never stored; each call gets its own id.
    /// </summary>
    public static InvoiceDocument New(string fileName = "FV-2026-0117.pdf")
    {
        var id = Guid.CreateVersion7();
        return InvoiceDocument.Create(id, $"tenants/test/documents/{id}", fileName, 48_213, new string('a', 64));
    }
}
